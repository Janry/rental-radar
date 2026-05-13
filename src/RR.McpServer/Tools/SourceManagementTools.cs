using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using RR.Core.Abstractions;
using RR.Core.Domain;

namespace RR.McpServer.Tools;

/// <summary>
/// Ручне керування джерелами скрапінгу (FB-групи, Marketplace категорії тощо).
///
/// Phase 3 свідомо побудована на ручному додаванні: користувач знає основні
/// FB-групи для своїх локацій. Auto-discovery (ISourceDiscoveryService) уже
/// має свій seam і буде підключений пізніше без зміни цих tools.
/// </summary>
[McpServerToolType]
public sealed class SourceManagementTools(
    IScrapeSourceRepository sources,
    ILocationRepository locations)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [McpServerTool(Name = "add_source")]
    [Description("Додати одне або декілька джерел скрапінгу (FB-групи) до локації. " +
                 "Приймає список URL — можна вставити одразу пачку. " +
                 "Дублікати в межах локації пропускаються, а не помилка.")]
    public async Task<string> AddSourceAsync(
        [Description("ID або slug локації ('ko-phangan')")] string location,
        [Description("Список URL джерел. Приклад: 'https://facebook.com/groups/123' або '.../groups/koh-phangan-rentals'")] string[] urls,
        [Description("Тип джерела: FacebookGroup (дефолт), FacebookMarketplace")] string? type = null,
        CancellationToken ct = default)
    {
        var loc = await ResolveLocationAsync(location, ct);
        if (loc is null)
            return JsonSerializer.Serialize(new { error = $"Location '{location}' not found" });

        if (urls.Length == 0)
            return JsonSerializer.Serialize(new { error = "At least one URL required" });

        var sourceType = ParseType(type);

        var existing = await sources.GetByLocationAsync(loc.Id, ct);
        var existingUrls = existing.Select(s => s.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = new List<ScrapeSource>();
        var skipped = new List<object>();
        var errors = new List<object>();

        foreach (var url in urls.Select(u => u.Trim()).Where(u => u.Length > 0).Distinct())
        {
            if (!IsValidFacebookUrl(url))
            {
                errors.Add(new { url, reason = "Not a recognised Facebook URL" });
                continue;
            }

            if (existingUrls.Contains(url))
            {
                skipped.Add(new { url, reason = "Already added to this location" });
                continue;
            }

            added.Add(new ScrapeSource
            {
                LocationId = loc.Id,
                Url = url,
                Name = DeriveNameFromUrl(url),
                Type = sourceType,
                IsAutoDiscovered = false,
                IsEnabled = true,
                RelevanceScore = 1.0   // Ручне додавання — користувач сам вибрав, віримо.
            });
        }

        if (added.Count > 0)
            await sources.AddRangeAsync(added, ct);

        return JsonSerializer.Serialize(new
        {
            success = true,
            location = loc.Name,
            added = added.Select(s => new { id = s.Id, url = s.Url, name = s.Name, type = s.Type.ToString() }),
            skipped,
            errors,
            hint = added.Count > 0
                ? "Імена тимчасові — скрапер оновить їх при першому проході."
                : null
        }, JsonOpts);
    }

    [McpServerTool(Name = "list_sources")]
    [Description("Показати всі джерела скрапінгу для конкретної локації.")]
    public async Task<string> ListSourcesAsync(
        [Description("ID або slug локації")] string location,
        CancellationToken ct = default)
    {
        var loc = await ResolveLocationAsync(location, ct);
        if (loc is null)
            return JsonSerializer.Serialize(new { error = $"Location '{location}' not found" });

        var list = await sources.GetByLocationAsync(loc.Id, ct);

        return JsonSerializer.Serialize(new
        {
            location = loc.Name,
            count = list.Count,
            sources = list.Select(s => new
            {
                id = s.Id,
                name = s.Name,
                url = s.Url,
                type = s.Type.ToString(),
                enabled = s.IsEnabled,
                auto_discovered = s.IsAutoDiscovered,
                last_scraped_at = s.LastScrapedAt,
                consecutive_failures = s.ConsecutiveFailures
            })
        }, JsonOpts);
    }

    [McpServerTool(Name = "set_source_enabled")]
    [Description("Увімкнути або вимкнути джерело без видалення. " +
                 "Вимкнене джерело пропускається скрапером, але історія + статистика зберігаються.")]
    public async Task<string> SetSourceEnabledAsync(
        [Description("ID джерела (GUID)")] string sourceId,
        [Description("true — увімкнути, false — вимкнути")] bool enabled,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(sourceId, out var id))
            return JsonSerializer.Serialize(new { error = "Invalid source ID" });

        var source = await sources.GetByIdAsync(id, ct);
        if (source is null)
            return JsonSerializer.Serialize(new { error = "Source not found" });

        source.IsEnabled = enabled;
        await sources.UpdateAsync(source, ct);

        return JsonSerializer.Serialize(new { success = true, id = source.Id, enabled }, JsonOpts);
    }

    [McpServerTool(Name = "remove_source")]
    [Description("Повністю видалити джерело разом з усіма його оголошеннями (cascade). " +
                 "Якщо хочеш просто паузу — використовуй set_source_enabled.")]
    public async Task<string> RemoveSourceAsync(
        [Description("ID джерела (GUID)")] string sourceId,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(sourceId, out var id))
            return JsonSerializer.Serialize(new { error = "Invalid source ID" });

        var source = await sources.GetByIdAsync(id, ct);
        if (source is null)
            return JsonSerializer.Serialize(new { error = "Source not found" });

        await sources.DeleteAsync(id, ct);

        return JsonSerializer.Serialize(new
        {
            success = true,
            removed_id = id,
            removed_url = source.Url
        }, JsonOpts);
    }

    private async Task<Location?> ResolveLocationAsync(string idOrSlug, CancellationToken ct)
    {
        if (Guid.TryParse(idOrSlug, out var guid))
            return await locations.GetByIdAsync(guid, ct);

        return await locations.GetBySlugAsync(idOrSlug.ToLowerInvariant(), ct);
    }

    private static ScrapeSourceType ParseType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ScrapeSourceType.FacebookGroup;
        return Enum.TryParse<ScrapeSourceType>(raw, ignoreCase: true, out var t)
            ? t
            : ScrapeSourceType.FacebookGroup;
    }

    private static readonly Regex FacebookUrlRegex = new(
        @"^https?://(www\.)?(m\.)?facebook\.com/(groups|marketplace)/",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static bool IsValidFacebookUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out _) && FacebookUrlRegex.IsMatch(url);

    /// <summary>
    /// Тимчасове ім'я з URL: останній path-сегмент, без query/fragment.
    /// Phase 4 scraper замінить на реальне ім'я групи з FB.
    /// </summary>
    private static string DeriveNameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var last = segments.LastOrDefault();
            return string.IsNullOrWhiteSpace(last) ? url : last.Replace('-', ' ').Replace('_', ' ');
        }
        catch
        {
            return url;
        }
    }
}
