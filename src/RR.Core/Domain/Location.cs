namespace RR.Core.Domain;

/// <summary>
/// Локація моніторингу: місто, острів або район.
/// Це КОРЕНЕВА сутність для всього проекту — від однієї Location залежать
/// і пошук джерел, і скрапінг, і фільтри користувачів.
///
/// Приклади:
///   Name="Ko Phangan", Country="TH", Areas=["Sri Thanu","Tong Sala","Haad Rin"]
///   Name="Canggu",     Country="ID", Areas=["Berawa","Pererenan","Echo Beach"]
///   Name="Lisbon",     Country="PT", Areas=["Alfama","Bairro Alto","Chiado"]
/// </summary>
public sealed class Location
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; init; }
    public required string Slug { get; init; }              // "ko-phangan", "canggu"
    public required string Country { get; init; }           // ISO-3166-1 alpha-2: "TH", "ID"

    public string Currency { get; init; } = "USD";          // ISO-4217: "THB", "IDR", "EUR"
    public string Timezone { get; init; } = "UTC";          // IANA: "Asia/Bangkok"

    /// <summary>
    /// Райони/області в межах локації — вживатимуться як для фільтрування,
    /// так і для генерації пошукових запитів автодискавері.
    /// </summary>
    public List<string> Areas { get; init; } = new();

    /// <summary>
    /// Базові ключові слова для пошуку джерел.
    /// Генеруються автоматично з Name + Areas, користувач може доповнити.
    /// </summary>
    public List<string> SearchKeywords { get; init; } = new();

    /// <summary>Джерела моніторингу — FB групи, Marketplace категорії тощо.</summary>
    public List<ScrapeSource> Sources { get; init; } = new();

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? LastDiscoveryAt { get; set; }          // коли останній раз шукали нові джерела
}
