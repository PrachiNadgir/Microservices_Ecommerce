using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProductService.Data
{
	public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
	{
		public AppDbContext CreateDbContext(string[] args)
		{
			var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

			optionsBuilder.UseNpgsql(
				"Host=localhost;Database=TestDb;Username=postgres;Password=1234"
			);

			return new AppDbContext(optionsBuilder.Options);
		}
	}
}