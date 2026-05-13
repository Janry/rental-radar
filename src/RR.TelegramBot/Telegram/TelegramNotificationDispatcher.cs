using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using RR.Core.Abstractions;
using RR.Core.Domain;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace RR.TelegramBot.Telegram;

/// <summary>
/// Шле повідомлення в TG. HTML parse mode щоб не возитися з екрануванням MarkdownV2.
/// Якщо є фото — шлемо `sendPhoto` з caption, інакше — `sendMessage`.
/// Telegram ліміт caption ~1024 символи; truncate raw_text при потребі.
/// </summary>
public sealed class TelegramNotificationDispatcher(
    ITelegramBotClient bot,
    ILogger<TelegramNotificationDispatcher> log)
    : INotificationDispatcher
{
    private const int MaxCaptionLength = 1020;   // Telegram limit 1024, з запасом 4 на ellipsis

    public async Task NotifyAsync(UserFilter filter, Listing listing, CancellationToken ct = default)
    {
        var caption = FormatCaption(filter, listing);

        try
        {
            if (listing.ImageUrls.Count > 0)
            {
                await bot.SendPhoto(
                    chatId: filter.TelegramChatId,
                    photo: InputFile.FromUri(listing.ImageUrls[0]),
                    caption: caption,
                    parseMode: ParseMode.Html,
                    cancellationToken: ct);
            }
            else
            {
                await bot.SendMessage(
                    chatId: filter.TelegramChatId,
                    text: caption,
                    parseMode: ParseMode.Html,
                    cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Failed to send TG notif to chat {ChatId} for listing {ListingId}",
                filter.TelegramChatId, listing.Id);
            throw;
        }
    }

    internal static string FormatCaption(UserFilter filter, Listing listing)
    {
        var sb = new StringBuilder();
        sb.Append($"<b>🏠 Новий збіг — {WebUtility.HtmlEncode(filter.Name)}</b>\n\n");

        if (listing.PricePerMonth.HasValue)
            sb.Append($"💰 <b>{listing.PricePerMonth:N0}</b> / міс\n");

        if (!string.IsNullOrEmpty(listing.Area))
            sb.Append($"📍 {WebUtility.HtmlEncode(listing.Area)}\n");

        var details = new List<string>();
        if (listing.PropertyType.HasValue) details.Add(listing.PropertyType.Value.ToString());
        if (listing.Bedrooms.HasValue) details.Add($"{listing.Bedrooms} bed");
        if (listing.PetsAllowed == true) details.Add("🐾 pets ok");
        if (listing.HasPool == true) details.Add("🏊 pool");
        if (listing.HasHotWater == true) details.Add("🚿 hot water");
        if (listing.HasWifi == true) details.Add("📶 wifi");
        if (details.Count > 0) sb.Append(string.Join(" · ", details) + "\n");

        sb.Append('\n');
        sb.Append(WebUtility.HtmlEncode(Truncate(listing.RawText, 400)));

        if (!string.IsNullOrEmpty(listing.SourceUrl))
            sb.Append($"\n\n<a href=\"{WebUtility.HtmlEncode(listing.SourceUrl)}\">Перейти до посту</a>");

        var caption = sb.ToString();
        return caption.Length > MaxCaptionLength ? caption[..MaxCaptionLength] + "..." : caption;
    }

    private static string Truncate(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "...";
}
