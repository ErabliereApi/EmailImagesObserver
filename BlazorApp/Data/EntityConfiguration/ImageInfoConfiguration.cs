using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlazorApp.Data.EntityConfiguration;

public class ImageInfoConfiguration : IEntityTypeConfiguration<ImageInfo>
{
    public void Configure(EntityTypeBuilder<ImageInfo> builder)
    {
        
    }
}
