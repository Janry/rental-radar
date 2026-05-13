using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RR.Core.Abstractions;
using RR.Core.Domain;

namespace RR.McpServer.Tools;

[McpServerToolType]
public sealed class RentalSearchTools(
    IListingRepository listings,
    ILocationRepository locations)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [McpServerTool(Name = "search_rentals")]
    [Description("Шукати оголошення про оренду житла в межах певної локації. " +
                 "Повертає структуровані результати з ціною, локацією, контактами та URL.")]
    public async Task<string> SearchRentalsAsync(
        [Description("ID або slug локації ('ko-phangan', 'canggu')")] string location,
        [Description("Максимальна ціна за місяць у валюті локації")] decimal? maxPrice = null,
        [Description("Мінімальна ціна за місяць у валюті локації")] decimal? minPrice = null,
        [Description("Райони в межах локації")] string[]? areas = null,
        [Description("Типи житла: Bungalow, House, Apartment, Studio, Villa, Room")] string[]? propertyTypes = null,
        [Description("Тільки з дозволом на тварин")] bool? petsAllowed = null,
        [Description("Скільки днів назад шукати (дефолт 7)")] int? withinDays = 7,
        [Description("Максимум результатів (1-50, дефолт 20)")] int? limit = 20,
        CancellationToken ct = default)
    {
        var loc = await ResolveLocationAsync(location, ct);
        if (loc is null)
            return JsonSerializer.Serialize(new { error = $"Location '{location}' not found" });

        var criteria = new ListingSearchCriteria
        {
            LocationId = loc.Id,
            MaxPrice = maxPrice,
            MinPrice = minPrice,
            Areas = areas,
            PropertyTypes = propertyTypes?
                .Where(p => Enum.TryParse<PropertyType>(p, true, out _))
                .Select(p => Enum.Parse<PropertyType>(p, true))
                .ToList(),
            PetsAllowed = petsAllowed,
            PostedAfter = withinDays.HasValue ? DateTime.UtcNow.AddDays(-withinDays.Value) : null,
            Limit = Math.Clamp(limit ?? 20, 1, 50)
        };

        var results = await listings.SearchAsync(criteria, ct);

        return JsonSerializer.Serialize(new
        {
            location = loc.Name,
            currency = loc.Currency,
            count = results.Count,
            listings = results.Select(l => new
            {
                id = l.Id,
                price = l.PricePerMonth,
                area = l.Area,
                bedrooms = l.Bedrooms,
                property_type = l.PropertyType?.ToString(),
                pets_allowed = l.PetsAllowed,
                has_pool = l.HasPool,
                has_hot_water = l.HasHotWater,
                posted_at = l.PostedAt,
                url = l.SourceUrl,
                preview = l.RawText.Length > 200 ? l.RawText[..200] + "..." : l.RawText,
                images = l.ImageUrls.Take(3),
                contacts = l.ContactInfo
            })
        }, JsonOpts);
    }

    [McpServerTool(Name = "get_listing_details")]
    [Description("Отримати повну інформацію про конкретне оголошення за його ID.")]
    public async Task<string> GetListingDetailsAsync(
        [Description("ID оголошення (GUID)")] string listingId,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(listingId, out var id))
            return JsonSerializer.Serialize(new { error = "Invalid listing ID" });

        var listing = await listings.GetByIdAsync(id, ct);
        return listing is null
            ? JsonSerializer.Serialize(new { error = "Listing not found" })
            : JsonSerializer.Serialize(listing, JsonOpts);
    }

    private async Task<Location?> ResolveLocationAsync(string idOrSlug, CancellationToken ct)
    {
        if (Guid.TryParse(idOrSlug, out var guid))
            return await locations.GetByIdAsync(guid, ct);

        return await locations.GetBySlugAsync(idOrSlug.ToLowerInvariant(), ct);
    }
}
