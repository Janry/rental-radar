using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.Core.Abstractions;
using RR.Infrastructure.Persistence;
using RR.Infrastructure.Scraping;

namespace RR.Scraper.Worker;

/// <summary>
/// Тонка обгортка: на старті — WAL + TTL cleanup; далі — цикл по таймеру.
/// Вся логіка одного проходу — у ScrapingPass.
/// </summary>
public sealed class ScrapingBackgroundService(
    IServiceScopeFactory scopeFactory,
    IFacebookSession session,
    ScrapingPass pass,
    IOptions<ScrapingOptions> options,
    ILogger<ScrapingBackgroundService> log)
    : BackgroundService
{
    private readonly ScrapingOptions _opts = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureWalModeAsync(stoppingToken);
        await PruneOldListingsAsync(stoppingToken);

        if (!await session.IsConfiguredAsync(stoppingToken))
            log.LogWarning(
                "FB session not configured — Worker йде в idle. Запустіть tools/FbLogin " +
                "і вкажіть FACEBOOK_SESSION_PATH. Worker не падає, чекає до наступного циклу.");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (await session.IsConfiguredAsync(stoppingToken))
            {
                try
                {
                    await pass.RunAsync(stoppingToken);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    log.LogError(ex, "Scraping pass failed");
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_opts.IntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task PruneOldListingsAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var listings = scope.ServiceProvider.GetRequiredService<IListingRepository>();
        var cutoff = DateTime.UtcNow.AddDays(-_opts.ListingRetentionDays);
        var deleted = await listings.DeleteOlderThanAsync(cutoff, ct);
        if (deleted > 0)
            log.LogInformation("TTL cleanup: removed {Count} listings older than {Days} days",
                deleted, _opts.ListingRetentionDays);
    }

    private async Task EnsureWalModeAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
    }
}
