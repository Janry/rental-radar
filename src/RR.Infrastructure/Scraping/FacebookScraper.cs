using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using RR.Core.Abstractions;
using RR.Core.Domain;

namespace RR.Infrastructure.Scraping;

/// <summary>
/// Playwright-based scraper для публічних FB-груп.
///
/// Lifecycle:
///   - Singleton DI: один Playwright + один IBrowser живуть весь час Worker-а
///   - Свіжий IBrowserContext (з storageState) — на КОЖЕН виклик ScrapeAsync,
///     щоб FB бачила "новий" peer per source і не корелювала між групами
///   - DisposeAsync на shutdown Worker-а закриває Chromium
///
/// DOM-селектори FB міняються часто. Все що не входить у Playwright API
/// винесене у константи нижче — коли скрапер ламається, треба правити лише їх.
/// </summary>
public sealed class FacebookScraper(
    IFacebookSession session,
    ILogger<FacebookScraper> log)
    : IFacebookScraper, IAsyncDisposable
{
    // Сторінка з пар-сотнями постів важить ~50 MB DOM — обмежуємо memory і час.
    private const int MaxScrolls = 5;
    private const int ScrollDelayMs = 1500;
    private const int NavTimeoutMs = 30_000;
    private const int ArticleWaitMs = 15_000;

    // FB DOM-селектори (стан на 2026-05). При поломці — поправити тут.
    private const string ArticleSelector = "div[role='article']";
    private const string LoginWallIndicator = "form[action*='login']";

    private IPlaywright? _pw;
    private IBrowser? _browser;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public async IAsyncEnumerable<RawListing> ScrapeAsync(
        ScrapeSource source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var browser = await EnsureBrowserAsync(ct);
        await using var context = await session.CreateContextAsync(browser, ct);
        var page = await context.NewPageAsync();

        log.LogInformation("Scraping {SourceName} at {Url}", source.Name, source.Url);

        await page.GotoAsync(source.Url, new PageGotoOptions
        {
            Timeout = NavTimeoutMs,
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        // Якщо session протух — FB кидає на login. Краще явно зловити, ніж скролити порожню сторінку.
        if (await page.QuerySelectorAsync(LoginWallIndicator) is not null)
            throw new InvalidOperationException(
                $"FB redirected to login wall for {source.Url}. " +
                "Сесія застаріла — запустіть tools/FbLogin.");

        try
        {
            await page.WaitForSelectorAsync(ArticleSelector, new PageWaitForSelectorOptions
            {
                Timeout = ArticleWaitMs
            });
        }
        catch (TimeoutException)
        {
            log.LogWarning("No articles found on {Url} within {Ms}ms", source.Url, ArticleWaitMs);
            yield break;
        }

        for (var i = 0; i < MaxScrolls && !ct.IsCancellationRequested; i++)
        {
            await page.EvaluateAsync("window.scrollBy(0, window.innerHeight)");
            await Task.Delay(ScrollDelayMs, ct);
        }

        var articles = await page.QuerySelectorAllAsync(ArticleSelector);
        log.LogInformation("Found {Count} article elements on {Url}", articles.Count, source.Url);

        var seenIds = new HashSet<string>();

        foreach (var article in articles)
        {
            ct.ThrowIfCancellationRequested();

            RawListing? raw;
            try
            {
                raw = await ParseArticleAsync(article, source.Id);
            }
            catch (Exception ex)
            {
                log.LogDebug(ex, "Skipping unparseable article on {Url}", source.Url);
                continue;
            }

            if (raw is null) continue;
            if (!seenIds.Add(raw.ExternalId)) continue;

            yield return raw;
        }
    }

    private static async Task<RawListing?> ParseArticleAsync(IElementHandle article, Guid sourceId)
    {
        var text = (await article.InnerTextAsync()).Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length < 30)
            return null;

        // Author: перший лінк з невеликим текстом, типово в шапці карти поста.
        var authorEl = await article.QuerySelectorAsync("h2 a, h3 a, h4 a");
        var authorName = authorEl is null ? null : (await authorEl.InnerTextAsync()).Trim();
        var authorUrl = authorEl is null ? null : await authorEl.GetAttributeAsync("href");

        // Images: збираємо реальні CDN-урли (scontent — FB image CDN).
        var imageHandles = await article.QuerySelectorAllAsync("img[src*='scontent']");
        var imageUrls = new List<string>();
        foreach (var img in imageHandles)
        {
            var src = await img.GetAttributeAsync("src");
            if (!string.IsNullOrEmpty(src)) imageUrls.Add(src);
        }

        // Permalink на пост — будь-який <a> з href що містить /posts/ або /permalink/.
        var permalinkEl = await article.QuerySelectorAsync("a[href*='/posts/'], a[href*='/permalink/']");
        var sourceUrl = permalinkEl is null ? null : await permalinkEl.GetAttributeAsync("href");
        sourceUrl = NormalizeUrl(sourceUrl);

        // FB прибрав стабільний post-id з публічного DOM. Хешуємо стабільний composite:
        //   author + початок тексту. Колізії можливі, але acceptable для дедупа.
        var externalId = DeriveExternalId(authorName, text);

        return new RawListing(
            SourceId: sourceId,
            ExternalId: externalId,
            SourceUrl: sourceUrl ?? string.Empty,
            Text: text,
            AuthorName: authorName,
            AuthorProfileUrl: NormalizeUrl(authorUrl),
            ImageUrls: imageUrls,
            // FB time parsing — окрема морока, поки використовуємо ScrapedAt-now.
            // Phase 5 матиме AI який витягне дату з тексту якщо є.
            PostedAt: DateTime.UtcNow);
    }

    private static string DeriveExternalId(string? author, string text)
    {
        var preview = text.Length > 200 ? text[..200] : text;
        var input = $"{author ?? "anon"}|{preview}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();   // 16 hex chars
    }

    private static string? NormalizeUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return url;
        if (url.StartsWith("/")) return $"https://www.facebook.com{url}";
        return url;
    }

    private async Task<IBrowser> EnsureBrowserAsync(CancellationToken ct)
    {
        if (_browser is not null) return _browser;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_browser is not null) return _browser;

            _pw = await Playwright.CreateAsync();
            _browser = await _pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = [
                    "--disable-blink-features=AutomationControlled",
                    "--no-sandbox"
                ]
            });
            log.LogInformation("Chromium launched");
            return _browser;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
            _browser = null;
        }
        _pw?.Dispose();
        _pw = null;
        _initLock.Dispose();
    }
}
