using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RR.Core.Abstractions;
using RR.Infrastructure;
using RR.Infrastructure.Matching;
using RR.TelegramBot;
using RR.TelegramBot.Telegram;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));

// Bot token: краще через env-var TELEGRAM_BOT_TOKEN, ніж зберігати в appsettings.
builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;
    var token = !string.IsNullOrEmpty(opts.BotToken)
        ? opts.BotToken
        : builder.Configuration["TELEGRAM_BOT_TOKEN"]
          ?? throw new InvalidOperationException(
              "Telegram bot token не сконфігурований. Задайте Telegram:BotToken " +
              "у appsettings або env TELEGRAM_BOT_TOKEN.");
    return new TelegramBotClient(token);
});

builder.Services.AddSingleton<IMatchingEngine, MatchingEngine>();
builder.Services.AddSingleton<INotificationDispatcher, TelegramNotificationDispatcher>();

builder.Services.AddHostedService<NotificationDispatchService>();
builder.Services.AddHostedService<BotPollingService>();

await builder.Build().RunAsync();
