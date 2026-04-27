using ProductService.Data;
using ProductService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using ProductService.Hubs;

namespace ProductService.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConnectionMultiplexer? _redis=null;
        private readonly IHubContext<NotificationHub> _hub;
        private const string CACHE_KEY = "products";
        public ProductController(
            AppDbContext context,
            IHubContext<NotificationHub> hub,
            IServiceProvider serviceProvider   // ✅ use this
        )
        {
            _context = context;
            _hub = hub;

            // ✅ Try to resolve Redis safely
            _redis = serviceProvider.GetService<IConnectionMultiplexer>();
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            IDatabase? db = null;

            if (_redis != null)
            {
                try
                {
                    db = _redis.GetDatabase();

                    var cachedData = await db.StringGetAsync(CACHE_KEY);

                    if (!cachedData.IsNullOrEmpty)
                    {
                        var products = JsonSerializer.Deserialize<List<Product>>(cachedData!);
                        if (products != null)
                            return Ok(products);
                    }
                }
                catch { }
            }

            var data = await _context.Products.ToListAsync();

            if (db != null)
            {
                try
                {
                    await db.StringSetAsync(
                        CACHE_KEY,
                        JsonSerializer.Serialize(data),
                        TimeSpan.FromMinutes(5)
                    );
                }
                catch { }
            }

            return Ok(data);
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] ProductDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Name))
                return BadRequest("Invalid product");

            var product = new Product
            {
                Name = dto.Name,
                Price = dto.Price
            };

            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();

            if (_redis != null)
            {
                try
                {
                    var db = _redis.GetDatabase();
                    await db.KeyDeleteAsync(CACHE_KEY);
                }
                catch { }
            }

            // ✅ SignalR notification
            await _hub.Clients.All.SendAsync("ReceiveNotification", $"Product Added: {product.Name}");

            return Ok(product);
        }
    }
}