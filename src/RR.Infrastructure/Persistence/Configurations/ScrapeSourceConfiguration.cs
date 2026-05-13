using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RR.Core.Domain;

namespace RR.Infrastructure.Persistence.Configurations;

internal sealed class ScrapeSourceConfiguration : IEntityTypeConfiguration<ScrapeSource>
{
    public void Configure(EntityTypeBuilder<ScrapeSource> builder)
    {
        builder.ToTable("scrape_sources");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Url).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(x => new { x.LocationId, x.Url }).IsUnique();
        builder.HasIndex(x => x.LocationId);
    }
}
