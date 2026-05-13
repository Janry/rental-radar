using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RR.Core.Domain;

namespace RR.Infrastructure.Persistence.Configurations;

internal sealed class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.ToTable("listings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExternalId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.SourceUrl).IsRequired().HasMaxLength(500);
        builder.Property(x => x.RawText).IsRequired();
        builder.Property(x => x.AuthorName).HasMaxLength(200);
        builder.Property(x => x.AuthorProfileUrl).HasMaxLength(500);
        builder.Property(x => x.Area).HasMaxLength(200);

        builder.Property(x => x.PropertyType)
            .HasConversion<string?>()
            .HasMaxLength(32);

        builder.Property(x => x.ImageUrls)
            .HasConversion(JsonValueConverters.StringList)
            .Metadata.SetValueComparer(JsonValueConverters.StringListComparer);

        builder.Property(x => x.ContactInfo)
            .HasConversion(JsonValueConverters.StringList)
            .Metadata.SetValueComparer(JsonValueConverters.StringListComparer);

        // Dedup key: один пост з одного джерела не повинен з'являтись двічі.
        builder.HasIndex(x => new { x.SourceId, x.ExternalId }).IsUnique();

        // Запити "останні оголошення в локації" — типовий патерн.
        builder.HasIndex(x => new { x.LocationId, x.PostedAt })
            .IsDescending(false, true);

        // TTL cleanup сканує цей індекс.
        builder.HasIndex(x => x.ScrapedAt);

        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ScrapeSource>()
            .WithMany()
            .HasForeignKey(x => x.SourceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
