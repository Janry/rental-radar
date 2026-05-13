using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.Core.Abstractions;
using RR.Core.Domain;

namespace RR.Scraper.Worker;

/// <summary>
/// Один прохід скрапера: пройти всі enabled sources, повернути summary.
/// Окремий клас (а не метод у BackgroundService) щоб легко тестувати без таймера.
/// </summary>
public sealed class ScrapingPass(
    IServiceScopeFactory scopeFactory,
    IOptions<ScrapingOptions> options,
    ILogger<ScrapingPass> log)
{
    private readonly ScrapingOptions _opts = options.Value;
    private readonly Random _rng = new();

    public async Task<PassSummary> RunAsync(CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sources = scope.ServiceProvider.GetRequiredService<IScrapeSourceRepository>();
        var locations = scope.ServiceProvider.GetRequiredService<ILocationRepository>();
        var listings = scope.ServiceProvider.GetRequiredService<IListingRepository>();
        var extractor = scope.ServiceProvider.GetRequiredService<IAiListingExtractor>();
        var scraper = scope.ServiceProvider.GetRequiredService<IFacebookScraper>();

        var enabled = await sources.GetEnabledAsync(ct);
        log.LogInformation("Pass start — {Count} enabled sources", enabled.Count);

        var summary = new PassSummary();
        var first = true;

        foreach (var source in enabled)
        {
            if (ct.IsCancellationRequested) break;

            if (!first) await RandomDelayAsync(ct);
            first = false;

            var location = await locations.GetByIdAsync(source.LocationId, ct);
            if (location is null)
            {
                log.LogWarning("Source {SourceId} points to non-existent location {LocationId}",
                    source.Id, source.LocationId);
                continue;
            }

            var sourceResult = await ScrapeOneAsync(source, location, scraper, listings, extractor, sources, ct);
            summary.SourceResults.Add(sourceResult);
        }

        log.LogInformation("Pass complete — {Sources} sources, +{Added} new listings",
            summary.SourceResults.Count, summary.TotalAdded);
        return summary;
    }

    private async Task<SourceResult> ScrapeOneAsync(
        ScrapeSource source,
        Location location,
        IFacebookScraper scraper,
        IListingRepository listings,
        IAiListingExtractor extractor,
        IScrapeSourceRepository sources,
        CancellationToken ct)
    {
        var added = 0;
        var skipped = 0;

        try
        {
            await foreach (var raw in scraper.ScrapeAsync(source, ct))
            {
                if (await listings.ExistsByExternalIdAsync(source.Id, raw.ExternalId, ct))
                {
                    skipped++;
                    continue;
                }

                var listing = await extractor.ExtractAsync(raw, location, ct);
                if (listing is null) continue;

                await listings.AddAsync(listing, ct);
                added++;
            }

            source.LastScrapedAt = DateTime.UtcNow;
            source.LastSuccessAt = DateTime.UtcNow;
            source.ConsecutiveFailures = 0;
            await sources.UpdateAsync(source, ct);

            log.LogInformation("{Source}: +{Added} new / {Skipped} skipped", source.Name, added, skipped);
            return new SourceResult(source.Id, added, skipped, Disabled: false, Error: null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            source.LastScrapedAt = DateTime.UtcNow;
            source.ConsecutiveFailures++;
            var disabled = source.ConsecutiveFailures >= _opts.MaxConsecutiveFailures;
            if (disabled) source.IsEnabled = false;

            log.Log(disabled ? LogLevel.Warning : LogLevel.Error, ex,
                "{Source}: failed ({N}/{Max} consecutive){Auto}",
                source.Name, source.ConsecutiveFailures, _opts.MaxConsecutiveFailures,
                disabled ? " — auto-disabled" : "");

            await sources.UpdateAsync(source, ct);
            return new SourceResult(source.Id, 0, 0, disabled, ex.Message);
        }
    }

    private async Task RandomDelayAsync(CancellationToken ct)
    {
        var sec = _rng.Next(_opts.MinDelayBetweenSourcesSec, _opts.MaxDelayBetweenSourcesSec + 1);
        log.LogDebug("Sleeping {Sec}s before next source", sec);
        await Task.Delay(TimeSpan.FromSeconds(sec), ct);
    }
}

public sealed class PassSummary
{
    public List<SourceResult> SourceResults { get; } = new();
    public int TotalAdded => SourceResults.Sum(r => r.Added);
    public int TotalSkipped => SourceResults.Sum(r => r.Skipped);
}

public sealed record SourceResult(Guid SourceId, int Added, int Skipped, bool Disabled, string? Error);
