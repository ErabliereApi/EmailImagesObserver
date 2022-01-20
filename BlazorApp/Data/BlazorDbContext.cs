using Microsoft.EntityFrameworkCore;

namespace BlazorApp.Data;

public class BlazorDbContext : DbContext
{
    public BlazorDbContext(DbContextOptions options) : base(options)
    {

    }

    public DbSet<ImageInfo> ImagesInfo { get; set; }
}
