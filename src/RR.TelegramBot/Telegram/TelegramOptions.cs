namespace RR.TelegramBot.Telegram;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    /// <summary>Bot token from @BotFather. Краще передавати через env-var TELEGRAM_BOT_TOKEN.</summary>
    public string BotToken { get; init; } = "";

    /// <summary>Інтервал між опитуваннями БД на нові unprocessed listings.</summary>
    public int DispatchPollSeconds { get; init; } = 60;

    /// <summary>Скільки listings обробляти за один прохід.</summary>
    public int DispatchBatchSize { get; init; } = 50;

    /// <summary>Long-polling timeout для GetUpdates. 30s — стандарт для tg long-poll.</summary>
    public int UpdatesLongPollSeconds { get; init; } = 30;
}
