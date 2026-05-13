using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RR.Core.Domain;

namespace RR.Infrastructure.Persistence.Configurations;

internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Country).IsRequired().HasMaxLength(2);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Timezone).IsRequired().HasMaxLength(64);

        builder.HasIndex(x => x.Slug).IsUnique();

        builder.Property(x => x.Areas)
            .HasConversion(JsonValueConverters.StringList)
            .Metadata.SetValueComparer(JsonValueConverters.StringListComparer);

        builder.Property(x => x.SearchKeywords)
            .HasConversion(JsonValueConverters.StringList)
            .Metadata.SetValueComparer(JsonValueConverters.StringListComparer);

        builder.HasMany(x => x.Sources)
            .WithOne()
            .HasForeignKey(s => s.LocationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
