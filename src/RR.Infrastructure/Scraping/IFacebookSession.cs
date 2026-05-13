using Microsoft.Playwright;

namespace RR.Infrastructure.Scraping;

/// <summary>
/// Інкапсулює "звідки беруться FB-cookies". Тримаємо в Infrastructure (а не Core),
/// бо інтерфейс торкається Playwright-типу IBrowserContext — кладти в Core було б
/// порушенням Clean Architecture: Core не залежить ні від чого зовнішнього.
///
/// Default impl — FileBasedFacebookSession (читає storageState JSON з диску).
/// Тест-impl може повертати моковий контекст без реального Chromium.
/// </summary>
public interface IFacebookSession
{
    /// <summary>
    /// Створити authenticated browser context на основі збереженої сесії.
    /// Викидає InvalidOperationException якщо session-файл відсутній/порожній.
    /// </summary>
    Task<IBrowserContext> CreateContextAsync(IBrowser browser, CancellationToken ct = default);

    /// <summary>
    /// Чи виглядає session-файл валідним? Перевіряє існування і базову структуру
    /// без мережевого запиту до FB (для нього потрібен Worker pass).
    /// </summary>
    Task<bool> IsConfiguredAsync(CancellationToken ct = default);
}
