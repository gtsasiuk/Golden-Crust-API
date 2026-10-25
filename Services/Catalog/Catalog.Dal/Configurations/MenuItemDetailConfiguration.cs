using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class MenuItemDetailConfiguration : IEntityTypeConfiguration<MenuItemDetail>
{
    public void Configure(EntityTypeBuilder<MenuItemDetail> builder)
    {
        builder.ToTable("MenuItemDetails", t =>
        {
            t.HasCheckConstraint("CK_MenuItemDetails_Weight", "[WeightGrams] IS NULL OR [WeightGrams] > 0");
            t.HasCheckConstraint("CK_MenuItemDetails_Calories", "[Calories] IS NULL OR [Calories] >= 0");
        });

        builder.HasKey(d => d.MenuItemId);

        builder.Property(d => d.Description).HasMaxLength(MenuItemDetail.DescriptionMaxLength);
        builder.Property(d => d.Ingredients).HasMaxLength(MenuItemDetail.IngredientsMaxLength);
        builder.Property(d => d.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}