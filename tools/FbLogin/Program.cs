using Microsoft.Playwright;

// Одноразова утиліта: відкриває Chromium у НЕ-headless режимі, ви руками логінитесь
// (з 2FA якщо є), потім тиснете ENTER — і ми дампимо session JSON для скрапера.
//
// Запуск: dotnet run --project tools/FbLogin -- path/to/fb-session.json

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/FbLogin -- <output-session.json>");
    Environment.Exit(1);
}

var outputPath = Path.GetFullPath(args[0]);
var outputDir = Path.GetDirectoryName(outputPath);
if (!string.IsNullOrEmpty(outputDir)) Directory.CreateDirectory(outputDir);

Console.WriteLine("Launching Chromium (non-headless)...");

// Browsers потрібно встановити один раз: `dotnet tool install --local Microsoft.Playwright.CLI`
// + `dotnet playwright install chromium`. Якщо не зроблено — Playwright.CreateAsync
// підкаже клікабельне повідомлення.
using var pw = await Playwright.CreateAsync();
await using var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = false
});
await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
{
    ViewportSize = new ViewportSize { Width = 1280, Height = 900 }
});

var page = await context.NewPageAsync();
await page.GotoAsync("https://www.facebook.com/login");

Console.WriteLine();
Console.WriteLine("=================================================================");
Console.WriteLine("  1. Залогіньтесь у Facebook у відкритому вікні (з 2FA якщо є).");
Console.WriteLine("  2. Дочекайтесь що сторінка перейшла на головний feed.");
Console.WriteLine("  3. Поверніться сюди і натисніть ENTER щоб зберегти сесію.");
Console.WriteLine("=================================================================");
Console.WriteLine();
Console.ReadLine();

await context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = outputPath });
Console.WriteLine($"Saved session → {outputPath}");
Console.WriteLine("Тепер вкажіть цей шлях у appsettings/env: FACEBOOK_SESSION_PATH");
