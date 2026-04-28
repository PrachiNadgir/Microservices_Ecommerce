using ProductService.Data;
using ProductService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using ProductService.Hubs;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ProductService.Controllers
{
    [Authorize(Roles = "Admin")] // all endpoints require login
    [ApiController]
    [Route("api/products")]
    public class ProductController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConnectionMultiplexer? _redis;
        private readonly IHubContext<NotificationHub> _hub;

        private const string CACHE_KEY = "products";

        public ProductController(
            AppDbContext context,
            IHubContext<NotificationHub> hub,
            IServiceProvider serviceProvider)
        {
            _context = context;
            _hub = hub;
            _redis = serviceProvider.GetService<IConnectionMultiplexer>();
        }

        // 🔹 GET PRODUCTS
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            if (_redis != null)
            {
                try
                {
                    var db = _redis.GetDatabase();
                    var cached = await db.StringGetAsync(CACHE_KEY);

                    if (!cached.IsNullOrEmpty)
                    {
                        var data = JsonSerializer.Deserialize<List<Product>>(cached!);
                        if (data != null)
                            return Ok(data);
                    }
                }
                catch { }
            }

            var products = await _context.Products.ToListAsync();

            if (_redis != null)
            {
                try
                {
                    var db = _redis.GetDatabase();
                    await db.StringSetAsync(CACHE_KEY, JsonSerializer.Serialize(products), TimeSpan.FromMinutes(5));
                }
                catch { }
            }

            return Ok(products);
        }

        // 🔹 GET NOTIFICATIONS
        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications()
        {
            var data = await _context.Notifications
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return Ok(data);
        }

        // 🔹 ADD PRODUCT (ADMIN ONLY)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Add([FromBody] ProductDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Invalid product");

            var product = new Product
            {
                Name = dto.Name,
                Price = dto.Price
            };

            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();

            // ❌ Clear cache
            if (_redis != null)
            {
                try
                {
                    var db = _redis.GetDatabase();
                    await db.KeyDeleteAsync(CACHE_KEY);
                }
                catch { }
            }

            // 🔔 Send SignalR notification
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                await _hub.Clients.User(userId).SendAsync("ReceiveNotification", new
                {
                    type = "PRODUCT_ADDED",
                    name = product.Name,
                    price = product.Price,
                    time = DateTime.UtcNow
                });
            }

            // 💾 Save notification
            _context.Notifications.Add(new Notification
            {
                Message = $"Product Added: {product.Name}",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return Ok(product);
        }
    }
}