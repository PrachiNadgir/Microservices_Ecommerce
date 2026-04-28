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
var key = builder.Configuration["Jwt:Key"] ?? "TEMP_SECRET_KEY";

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

        // ✅ SignalR JWT support
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
            }
        };
    });

builder.Services.AddAuthorization();

// ✅ CORS (FIXED FOR LOCAL + RENDER)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
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

        return ConnectionMultiplexer.Connect(config);
    });
}

// ✅ SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

// ✅ Map userId → SignalR
builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();

// ✅ Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ProductService API",
        Version = "v1"
    });

    // 🔐 JWT Auth in Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var app = builder.Build();

app.UseRouting();

// ✅ CORS FIRST


app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ✅ SignalR endpoint
app.MapHub<NotificationHub>("/notificationHub");

// ✅ Auto migration
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();