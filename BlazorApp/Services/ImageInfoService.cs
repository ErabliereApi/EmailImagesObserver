using BlazorApp.Data;
using Microsoft.EntityFrameworkCore;

namespace BlazorApp.Services;

public class ImageInfoService
{
    private readonly BlazorDbContext _context;

    public ImageInfoService(BlazorDbContext context)
    {
        _context = context;
    }

    public IAsyncEnumerable<ImageInfo> GetImageInfoAsync(int? take, int? skip = 0, string? search = null)
    {
        IQueryable<ImageInfo> query = _context.ImagesInfo.AsNoTracking();

        if (string.IsNullOrWhiteSpace(search) == false)
        {
            query = query.Where(i => i.AzureImageAPIInfo != null && i.AzureImageAPIInfo.Contains(search));
        }

        query = query.OrderByDescending(i => i.DateAjout);

        if (skip.HasValue)
        {
            query = query.Skip(skip.Value);
        }

        if (take.HasValue)
        {
            query = query.Take(take.Value);
        }

        return query.AsAsyncEnumerable();
    }

    public ValueTask<ImageInfo?> GetImageInfoAsync(long id)
    {
        return _context.ImagesInfo.FindAsync(new object?[] { id });
    }

    public async Task DeleteImageInfoAsync(long id)
    {
        var imageInfo = await GetImageInfoAsync(id);

        if (imageInfo is not null)
        {
            _ = _context.ImagesInfo.Remove(imageInfo);

            await _context.SaveChangesAsync();
        }
    }
}