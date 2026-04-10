using ProductService.Data;
using StackExchange.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
var builder = WebApplication.CreateBuilder(args); 
var key = builder.Configuration["Jwt:Key"] ?? throw new Exception("JWT Key missing");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { options.TokenValidationParameters = new TokenValidationParameters 
    { ValidateIssuer = false, 
      ValidateAudience = false, 
      ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
    };
    });
builder.Services.AddAuthorization(); 
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
{
    var redisConnection = builder.Configuration["Redis:ConnectionString"]
                      ?? throw new Exception("Redis connection missing");
    var config = ConfigurationOptions.Parse(redisConnection);
    config.AbortOnConnectFail = false; 
    config.ConnectRetry = 5; 
    config.ConnectTimeout = 10000; 
    var connection = ConnectionMultiplexer.Connect(config); 
    Console.WriteLine("Redis Connected: " + connection.IsConnected); return connection; });
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()
    ));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();
app.UseRouting();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();