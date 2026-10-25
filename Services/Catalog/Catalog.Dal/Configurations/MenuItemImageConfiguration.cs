using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class MenuItemImageConfiguration : IEntityTypeConfiguration<MenuItemImage>
{
    public void Configure(EntityTypeBuilder<MenuItemImage> builder)
    {
        builder.ToTable("MenuItemImages");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Url).IsRequired().HasMaxLength(MenuItemImage.UrlMaxLength);
        builder.Property(i => i.AltText).HasMaxLength(MenuItemImage.AltTextMaxLength);
        builder.Property(i => i.IsPrimary).HasDefaultValue(false);
        builder.Property(i => i.SortOrder).HasDefaultValue(0);
        builder.Property(i => i.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(i => new { i.MenuItemId, i.SortOrder })
               .HasDatabaseName("IX_MenuItemImages_MenuItemId_SortOrder")
               .IncludeProperties(i => new { i.Url, i.IsPrimary });

        builder.HasIndex(i => i.MenuItemId)
               .IsUnique()
               .HasDatabaseName("UX_MenuItemImages_OnePrimary")
               .HasFilter("[IsPrimary] = 1");
    }
}