using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RR.TelegramBot.Telegram;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace RR.TelegramBot;

/// <summary>
/// Long-polling для incoming messages. Phase 6 MVP: лише `/start` —
/// відповідаємо chat_id-ом, щоб юзер міг його скопіювати в `create_notification_filter`.
/// Решта команд (/pause, /resume, /filters) — наступним коммітом за бажанням.
/// </summary>
public sealed class BotPollingService(
    ITelegramBotClient bot,
    IOptions<TelegramOptions> options,
    ILogger<BotPollingService> log)
    : BackgroundService
{
    private readonly TelegramOptions _opts = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("Bot polling started");

        int? offset = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await bot.GetUpdates(
                    offset: offset,
                    timeout: _opts.UpdatesLongPollSeconds,
                    cancellationToken: stoppingToken);

                foreach (var update in updates)
                {
                    offset = update.Id + 1;
                    await HandleUpdateAsync(update, stoppingToken);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (ApiRequestException ex)
            {
                log.LogError(ex, "Telegram API error during polling");
                await DelaySafely(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Polling loop error");
                await DelaySafely(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task HandleUpdateAsync(Update update, CancellationToken ct)
    {
        if (update.Message is not { } msg) return;
        if (string.IsNullOrEmpty(msg.Text)) return;

        var text = msg.Text.Trim();
        var chatId = msg.Chat.Id;

        if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            var reply =
                $"<b>RentalRadar</b>\n\n" +
                $"Ваш chat_id: <code>{chatId}</code>\n\n" +
                $"Скопіюйте це число і передайте у виклик <code>create_notification_filter</code> через Claude Desktop.\n" +
                $"Після створення фільтрів — нові оголошення приходитимуть сюди в реальному часі.";

            await bot.SendMessage(chatId, reply, parseMode: ParseMode.Html,
                cancellationToken: ct);

            log.LogInformation("Replied to /start from chat {ChatId}", chatId);
            return;
        }

        if (text.StartsWith("/help", StringComparison.OrdinalIgnoreCase))
        {
            await bot.SendMessage(chatId,
                "Поки що один корисний command: <code>/start</code> — отримати свій chat_id для створення фільтра.",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    private static async Task DelaySafely(TimeSpan delay, CancellationToken ct)
    {
        try { await Task.Delay(delay, ct); }
        catch (OperationCanceledException) { }
    }
}
