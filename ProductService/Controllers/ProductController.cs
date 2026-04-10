using ProductService.Data;
using ProductService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Linq.Expressions;
using System.Text.Json;

namespace ProductService.Controllers
{


    [ApiController]
    [Route("api/products")]
    public class ProductController : ControllerBase
    {

        private readonly AppDbContext _context;
        private readonly IConnectionMultiplexer _redis;
        private const string CACHE_KEY = "products";

        public ProductController(AppDbContext context, IConnectionMultiplexer redis)
        {
            _context = context;
            _redis = redis;
        }
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var db = _redis.GetDatabase();

            try
            {
                var cachedData = await db.StringGetAsync(CACHE_KEY);

                if (!cachedData.IsNullOrEmpty)
                {
                    Console.WriteLine("CACHE HIT");
                    var products = JsonSerializer.Deserialize<List<Product>>(cachedData.ToString());
                    return Ok(products);
                }
            }
            catch
            {
                Console.WriteLine("Redis failed");
            }

          
            var data = await _context.Products.ToListAsync();

            try
            {
                await db.StringSetAsync(
                    CACHE_KEY,
                    JsonSerializer.Serialize(data),
                    TimeSpan.FromMinutes(5)
                );
            }
            catch { }

            return Ok(data);
        }
        [HttpPost]
        public async Task<IActionResult> Add(ProductDto dto)
        {
            if (dto == null)
                return BadRequest("Invalid product");

            var product = new Product
            {
                Name = dto.Name,
                Price = dto.Price
            };

            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();

            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync("products");

            return Ok(product);
        }



        //        private static readonly List<Product> products = new List<Product>
        //{
        //    new () { Id = 1, Name = "Laptop", Price = 111000 },
        //    new () { Id = 2, Name = "Smartphone", Price = 60000 },
        //    new () { Id = 3, Name = "Headphones", Price = 25000 }
        //};
        //        [HttpGet]
        //         public IActionResult Get()
        //        {
        //            return Ok(products);
        //        }
        //        [HttpPost]
        //        public IActionResult Add([FromBody] List<Product> newProducts)
        //        {
        //            if (newProducts == null || !newProducts.Any())
        //            {
        //                return BadRequest("Product list is empty");
        //            }

        //            products.AddRange(newProducts);

        //            return Ok(products);
        //        }
    }
}
