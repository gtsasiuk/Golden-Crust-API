using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(Tag.NameMaxLength);
        builder.Property(t => t.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(t => t.Name).IsUnique().HasDatabaseName("UQ_Tags_Name");

        builder.Navigation(t => t.ItemTags).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasData(
            new { Id = 1L, Name = "Вегетаріанське", CreatedAt = SeedConstants.SeedDate },
            new { Id = 2L, Name = "Хіт", CreatedAt = SeedConstants.SeedDate },
            new { Id = 3L, Name = "Гостре", CreatedAt = SeedConstants.SeedDate });
    }
}