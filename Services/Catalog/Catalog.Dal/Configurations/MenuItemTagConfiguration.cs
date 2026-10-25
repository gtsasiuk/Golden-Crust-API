using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class MenuItemTagConfiguration : IEntityTypeConfiguration<MenuItemTag>
{
    public void Configure(EntityTypeBuilder<MenuItemTag> builder)
    {
        builder.ToTable("MenuItemTags");

        builder.HasKey(x => new { x.MenuItemId, x.TagId });
        builder.Property(x => x.AddedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(x => x.MenuItem).WithMany(m => m.ItemTags)
               .HasForeignKey(x => x.MenuItemId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Tag).WithMany(t => t.ItemTags)
               .HasForeignKey(x => x.TagId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TagId, x.MenuItemId })
               .HasDatabaseName("IX_MenuItemTags_TagId_MenuItemId");
    }
}