using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace RR.Infrastructure.Scraping;

public sealed class FileBasedFacebookSession(IConfiguration cfg) : IFacebookSession
{
    private readonly string _sessionPath = cfg["FACEBOOK_SESSION_PATH"]
        ?? throw new InvalidOperationException(
            "FACEBOOK_SESSION_PATH is not configured. " +
            "Запустіть tools/FbLogin і вкажіть шлях до отриманого JSON.");

    public async Task<IBrowserContext> CreateContextAsync(IBrowser browser, CancellationToken ct = default)
    {
        if (!File.Exists(_sessionPath))
            throw new InvalidOperationException(
                $"FB session file not found: {_sessionPath}. Запустіть tools/FbLogin для оновлення.");

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            StorageStatePath = _sessionPath,
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                        "(KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
            Locale = "en-US",
            TimezoneId = "Asia/Bangkok"
        });

        return context;
    }

    public Task<bool> IsConfiguredAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_sessionPath)) return Task.FromResult(false);

        try
        {
            // Quick sanity: storageState.json повинен мати JSON-об'єкт з полем "cookies".
            // Не парсимо схему повністю — на цьому етапі це overkill.
            using var stream = File.OpenRead(_sessionPath);
            using var doc = System.Text.Json.JsonDocument.Parse(stream);
            return Task.FromResult(doc.RootElement.TryGetProperty("cookies", out _));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
