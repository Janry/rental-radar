using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RR.Core.Abstractions;
using RR.Core.Domain;

namespace RR.McpServer.Tools;

[McpServerToolType]
public sealed class LocationTools(
    ILocationRepository locations,
    ISourceDiscoveryService discovery)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [McpServerTool(Name = "add_location")]
    [Description("Додати нову локацію для моніторингу (місто, острів, район). " +
                 "Автоматично шукає Facebook групи та інші джерела за назвою + ключовим словом 'rent'. " +
                 "Повертає список знайдених джерел для затвердження.")]
    public async Task<string> AddLocationAsync(
        [Description("Назва локації (англ): 'Ko Phangan', 'Canggu', 'Lisbon'")] string name,
        [Description("ISO-код країни: 'TH', 'ID', 'PT'")] string country,
        [Description("Райони/квартали для пошуку (опційно)")] string[]? areas = null,
        [Description("Валюта ISO-4217: 'THB', 'IDR', 'EUR' (дефолт USD)")] string? currency = null,
        [Description("Timezone IANA: 'Asia/Bangkok' (дефолт UTC)")] string? timezone = null,
        CancellationToken ct = default)
    {
        var slug = name.ToLowerInvariant().Replace(' ', '-');

        var existing = await locations.GetBySlugAsync(slug, ct);
        if (existing is not null)
        {
            return JsonSerializer.Serialize(new
            {
                error = "Location already exists",
                location_id = existing.Id,
                hint = "Use 'discover_sources' to refresh sources for this location."
            }, JsonOpts);
        }

        var location = new Location
        {
            Name = name,
            Slug = slug,
            Country = country.ToUpperInvariant(),
            Currency = currency ?? "USD",
            Timezone = timezone ?? "UTC",
            Areas = areas?.ToList() ?? new(),
            SearchKeywords = GenerateKeywords(name, areas)
        };

        await locations.AddAsync(location, ct);

        // Автодискавері джерел
        var candidates = await discovery.DiscoverAsync(location, maxCandidates: 15, ct);

        return JsonSerializer.Serialize(new
        {
            success = true,
            location_id = location.Id,
            location_name = location.Name,
            discovered_sources = candidates.Select(s => new
            {
                id = s.Id,
                name = s.Name,
                url = s.Url,
                type = s.Type.ToString(),
                members = s.MemberCount,
                relevance = Math.Round(s.RelevanceScore, 2)
            }),
            next_step = "Перевір знайдені джерела та активуй потрібні через 'enable_source'."
        }, JsonOpts);
    }

    [McpServerTool(Name = "list_locations")]
    [Description("Показати всі активні локації, що моніторяться.")]
    public async Task<string> ListLocationsAsync(CancellationToken ct = default)
    {
        var list = await locations.GetActiveAsync(ct);
        return JsonSerializer.Serialize(new
        {
            count = list.Count,
            locations = list.Select(l => new
            {
                id = l.Id,
                name = l.Name,
                slug = l.Slug,
                country = l.Country,
                areas_count = l.Areas.Count,
                sources_count = l.Sources.Count,
                active_sources = l.Sources.Count(s => s.IsEnabled)
            })
        }, JsonOpts);
    }

    [McpServerTool(Name = "discover_sources")]
    [Description("Перезапустити автопошук джерел (FB груп тощо) для існуючої локації. " +
                 "Корисно якщо нові групи з'явилися або старі стали неактивними.")]
    public async Task<string> DiscoverSourcesAsync(
        [Description("ID локації")] string locationId,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(locationId, out var id))
            return JsonSerializer.Serialize(new { error = "Invalid location ID" });

        var location = await locations.GetByIdAsync(id, ct);
        if (location is null)
            return JsonSerializer.Serialize(new { error = "Location not found" });

        var candidates = await discovery.DiscoverAsync(location, maxCandidates: 20, ct);

        return JsonSerializer.Serialize(new
        {
            location = location.Name,
            found = candidates.Count,
            sources = candidates.Select(s => new
            {
                id = s.Id,
                name = s.Name,
                url = s.Url,
                type = s.Type.ToString(),
                members = s.MemberCount,
                relevance = Math.Round(s.RelevanceScore, 2)
            })
        }, JsonOpts);
    }

    /// <summary>
    /// Базові ключові слова для пошуку джерел. Беремо:
    ///   "{name} rent", "{name} rental", "{name} accommodation"
    ///   + для кожного area: "{area} rent"
    /// </summary>
    private static List<string> GenerateKeywords(string name, string[]? areas)
    {
        var keywords = new List<string>
        {
            $"{name} rent",
            $"{name} rental",
            $"{name} long term rental",
            $"{name} accommodation"
        };

        if (areas is not null)
            foreach (var area in areas)
                keywords.Add($"{area} rent");

        return keywords;
    }
}
