namespace ProductService.Models
{
    public class Product
    {
        public int Id { get; set; }   // Required for DB
        public string Name { get; set; }
        public double Price { get; set; }
    }
}