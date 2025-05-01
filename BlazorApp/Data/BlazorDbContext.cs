#nullable disable
using Microsoft.EntityFrameworkCore;
using BlazorApp.Model;
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

    public DbSet<Mapping> Mappings { get; set; }

    public DbSet<Alerte> Alertes { get; set; }

    public async Task<int> ClearImagesAsync(string email, CancellationToken token = default)
    {
        int saved = 0;

        while ((await ImagesInfo.CountAsync(token)) > 0)
        {
            var images = await ImagesInfo.Take(10).ToArrayAsync(token);

            ImagesInfo.RemoveRange(images);

            saved += await SaveChangesAsync(token);
        }

        var emailEntity = await EmailStates.FirstOrDefaultAsync(e => e.Email == email);

        emailEntity.MessagesCount = 0;
        emailEntity.Size = 0;

        saved += await SaveChangesAsync(token);

        return saved;
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BlazorDbContext).Assembly);
    }
}
