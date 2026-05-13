using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.Core.Abstractions;
using RR.Infrastructure.Persistence;
using RR.TelegramBot.Telegram;

namespace RR.TelegramBot;

/// <summary>
/// Polling-based dispatcher:
///   1) Беремо unprocessed listings (ProcessedAt == null)
///   2) Для кожного — knowing filters, що матчать (структурно + семантично якщо є query)
///   3) Шлемо нотифу кожному матчевому фільтру
///   4) Виставляємо ProcessedAt — щоб не обробляти повторно (idempotency)
///
/// Окремий процес з scraper-ом — крах TG-сервера не валить пайплайн скрапінгу.
/// </summary>
public sealed class NotificationDispatchService(
    IServiceScopeFactory scopeFactory,
    IOptions<TelegramOptions> options,
    ILogger<NotificationDispatchService> log)
    : BackgroundService
{
    private readonly TelegramOptions _opts = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureWalModeAsync(stoppingToken);

        log.LogInformation("Dispatcher start — poll every {Sec}s, batch={Batch}",
            _opts.DispatchPollSeconds, _opts.DispatchBatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDispatchPassAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                log.LogError(ex, "Dispatch pass failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_opts.DispatchPollSeconds), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunDispatchPassAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var listings = scope.ServiceProvider.GetRequiredService<IListingRepository>();
        var matcher = scope.ServiceProvider.GetRequiredService<IMatchingEngine>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

        var unprocessed = await listings.GetUnprocessedAsync(_opts.DispatchBatchSize, ct);
        if (unprocessed.Count == 0) return;

        log.LogDebug("Dispatch pass — {Count} unprocessed listings", unprocessed.Count);

        var sent = 0;
        foreach (var listing in unprocessed)
        {
            if (ct.IsCancellationRequested) break;

            var matches = await matcher.FindMatchingFiltersAsync(listing, ct);
            foreach (var filter in matches)
            {
                try
                {
                    await dispatcher.NotifyAsync(filter, listing, ct);
                    sent++;
                }
                catch (Exception ex)
                {
                    // Окремий збій нотифи не блокує дедалі.
                    // ProcessedAt все одно виставимо — інакше при тимчасовому TG-збої будемо спамити одне й те ж.
                    log.LogWarning(ex, "Failed to notify filter {FilterId} for listing {ListingId}",
                        filter.Id, listing.Id);
                }
            }

            listing.ProcessedAt = DateTime.UtcNow;
            await listings.UpdateAsync(listing, ct);
        }

        if (sent > 0)
            log.LogInformation("Dispatched {Sent} notifications across {Listings} listings",
                sent, unprocessed.Count);
    }

    private async Task EnsureWalModeAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
    }
}
