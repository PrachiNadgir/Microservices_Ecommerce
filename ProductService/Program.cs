using ProductService.Data;
using StackExchange.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using ProductService.Hubs;
using ProductService.Helpers;

var builder = WebApplication.CreateBuilder(args);

// ✅ JWT
var key = builder.Configuration["Jwt:Key"] ?? throw new Exception("JWT Key missing");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/notificationHub"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },

            OnTokenValidated = context =>
            {
                Console.WriteLine("✅ JWT Token Validated for user: " +
                    context.Principal?.Identity?.Name);

                return Task.CompletedTask;
            },

            OnAuthenticationFailed = context =>
            {
                Console.WriteLine("❌ JWT Authentication Failed: " + context.Exception.Message);
                return Task.CompletedTask;
            },

            OnChallenge = context =>
            {
                Console.WriteLine("⚠️ Unauthorized request to: " +
                    context.HttpContext.Request.Path);

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();


// ✅ CORS (VERY IMPORTANT FOR SIGNALR)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins("http://localhost:56210") // your frontend
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});


// ✅ Redis (OPTIONAL)
var redisConnection = builder.Configuration["Redis:ConnectionString"];

if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var config = ConfigurationOptions.Parse(redisConnection);
        config.AbortOnConnectFail = false;
        config.ConnectRetry = 5;
        config.ConnectTimeout = 10000;

        var connection = ConnectionMultiplexer.Connect(config);
        Console.WriteLine("Redis Connected: " + connection.IsConnected);

        return connection;
    });
}


// ✅ SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

// ✅ Custom UserIdProvider for SignalR

builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();


// ✅ Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


// ✅ Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseRouting();


// ✅ USE CORS HERE (IMPORTANT ORDER)
app.UseCors("AllowFrontend");


app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();


// ✅ SignalR Hub
app.MapHub<ProductService.Hubs.NotificationHub>("/notificationHub");


// ✅ Auto migration
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();