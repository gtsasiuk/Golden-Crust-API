using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class MenuCategoryConfiguration : IEntityTypeConfiguration<MenuCategory>
{
    public void Configure(EntityTypeBuilder<MenuCategory> builder)
    {
        builder.ToTable("MenuCategories");
        builder.ConfigureAudit();

        builder.Property(c => c.Name).IsRequired().HasMaxLength(MenuCategory.NameMaxLength);
        builder.Property(c => c.SortOrder).HasDefaultValue(0);

        builder.HasIndex(c => c.Name).IsUnique().HasDatabaseName("UQ_MenuCategories_Name");

        builder.Navigation(c => c.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasData(
            new { Id = 1L, Name = "Піци", SortOrder = 1, CreatedAt = SeedConstants.SeedDate, CreatedBy = SeedConstants.SeedUser, IsDeleted = false },
            new { Id = 2L, Name = "Салати", SortOrder = 2, CreatedAt = SeedConstants.SeedDate, CreatedBy = SeedConstants.SeedUser, IsDeleted = false },
            new { Id = 3L, Name = "Десерти", SortOrder = 3, CreatedAt = SeedConstants.SeedDate, CreatedBy = SeedConstants.SeedUser, IsDeleted = false },
            new { Id = 4L, Name = "Напої", SortOrder = 4, CreatedAt = SeedConstants.SeedDate, CreatedBy = SeedConstants.SeedUser, IsDeleted = false });
    }
}