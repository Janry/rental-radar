using RR.Core.Abstractions;
using RR.Core.Domain;

namespace RR.Infrastructure.Ai;

/// <summary>
/// Phase 4 placeholder — копіює сирий текст у Listing і виставляє мінімум обов'язкових
/// полів. Структуровані атрибути (PricePerMonth, Area, Bedrooms, тощо) залишаються null —
/// це робота Phase 5 (Claude API extraction).
///
/// Завдяки тому що search_rentals/get_listing_details уже працюють з nullable полями,
/// MCP-tools повертатимуть raw_text + URL, чого достатньо для перших smoke-тестів.
/// </summary>
public sealed class StubAiListingExtractor : IAiListingExtractor
{
    public Task<Listing?> ExtractAsync(RawListing raw, Location location, CancellationToken ct = default)
    {
        var listing = new Listing
        {
            LocationId = location.Id,
            SourceId = raw.SourceId,
            ExternalId = raw.ExternalId,
            SourceUrl = raw.SourceUrl,
            RawText = raw.Text,
            AuthorName = raw.AuthorName,
            AuthorProfileUrl = raw.AuthorProfileUrl,
            ImageUrls = raw.ImageUrls.ToList(),
            PostedAt = raw.PostedAt,
            ConfidenceScore = 0.0   // Phase 5 виставить реальний score
        };

        return Task.FromResult<Listing?>(listing);
    }
}
