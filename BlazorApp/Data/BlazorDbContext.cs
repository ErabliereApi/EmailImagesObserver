#nullable disable
using Microsoft.EntityFrameworkCore;
namespace BlazorApp.Data;

public class BlazorDbContext : DbContext
{
    /// <summary>
    /// Constructeur requis pour le passages des options
    /// </summary>
    /// <param name="options"></param>
    public BlazorDbContext(DbContextOptions options) : base(options)
    {

    }

    public DbSet<ImageInfo> ImagesInfo { get; set; }

    public DbSet<EmailStates> EmailStates { get; set; }
}
