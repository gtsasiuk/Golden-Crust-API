using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("MenuItems", t =>
            t.HasCheckConstraint("CK_MenuItems_Price", "[Price] > 0"));
        builder.ConfigureAudit();

        builder.Property(m => m.Name).IsRequired().HasMaxLength(MenuItem.NameMaxLength);
        builder.Property(m => m.Price).HasColumnType("decimal(10,2)");

        builder.HasOne(m => m.Category).WithMany(c => c.Items)
               .HasForeignKey(m => m.CategoryId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Detail).WithOne()
               .HasForeignKey<MenuItemDetail>(d => d.MenuItemId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Images).WithOne()
               .HasForeignKey(i => i.MenuItemId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(m => m.Images).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(m => m.ItemTags).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(m => new { m.CategoryId, m.Name })
               .HasDatabaseName("IX_MenuItems_CategoryId_Name")
               .IncludeProperties(m => new { m.Price, m.IsAvailable })
               .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(m => m.Name)
               .IsUnique()
               .HasDatabaseName("UX_MenuItems_Name")
               .HasFilter("[IsDeleted] = 0");
    }
}