using RR.Core.Domain;

namespace RR.Core.Abstractions;

public interface IListingRepository
{
    Task<bool> ExistsByExternalIdAsync(Guid sourceId, string externalId, CancellationToken ct = default);
    Task AddAsync(Listing listing, CancellationToken ct = default);
    Task UpdateAsync(Listing listing, CancellationToken ct = default);
    Task<Listing?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Listing>> SearchAsync(ListingSearchCriteria criteria, CancellationToken ct = default);
    Task<IReadOnlyList<Listing>> GetUnprocessedAsync(int limit, CancellationToken ct = default);

    /// <summary>
    /// Видаляє listings, де ScrapedAt раніше за cutoff. Викликається періодично
    /// scraper-ом для TTL-cleanup (типово ~30 днів). Повертає к-ть видалених рядків.
    /// </summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default);
}

public interface IUserFilterRepository
{
    Task<UserFilter?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<UserFilter>> GetActiveAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UserFilter>> GetActiveByLocationAsync(Guid locationId, CancellationToken ct = default);
    Task<IReadOnlyList<UserFilter>> GetByChatIdAsync(long telegramChatId, CancellationToken ct = default);
    Task AddAsync(UserFilter filter, CancellationToken ct = default);
    Task UpdateAsync(UserFilter filter, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ILocationRepository
{
    Task<Location?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Location?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<Location>> GetActiveAsync(CancellationToken ct = default);
    Task AddAsync(Location location, CancellationToken ct = default);
    Task UpdateAsync(Location location, CancellationToken ct = default);
}

public interface IScrapeSourceRepository
{
    Task<ScrapeSource?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ScrapeSource>> GetByLocationAsync(Guid locationId, CancellationToken ct = default);
    Task<IReadOnlyList<ScrapeSource>> GetEnabledAsync(CancellationToken ct = default);
    Task AddAsync(ScrapeSource source, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<ScrapeSource> sources, CancellationToken ct = default);
    Task UpdateAsync(ScrapeSource source, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Знаходить FB групи / маркетплейс категорії / інші джерела
/// для нової локації. Викликається при додаванні Location або періодично
/// для оновлення списку джерел.
/// </summary>
public interface ISourceDiscoveryService
{
    /// <summary>
    /// Знайти кандидатів-джерел для локації. Не зберігає в БД —
    /// повертає для затвердження користувачем.
    /// </summary>
    Task<IReadOnlyList<ScrapeSource>> DiscoverAsync(
        Location location,
        int maxCandidates = 20,
        CancellationToken ct = default);
}

public interface IFacebookScraper
{
    /// <summary>Зайти у вказане джерело і витягти нові пости.</summary>
    IAsyncEnumerable<RawListing> ScrapeAsync(ScrapeSource source, CancellationToken ct = default);
}

public interface IAiListingExtractor
{
    /// <summary>
    /// Перетворює сирий текст поста на структуроване оголошення через Claude API.
    /// Повертає null, якщо це не оголошення про оренду або не підходить для локації.
    /// </summary>
    Task<Listing?> ExtractAsync(RawListing raw, Location location, CancellationToken ct = default);
}

public interface IMatchingEngine
{
    /// <summary>Знайти всі фільтри, що матчать оголошення.</summary>
    Task<IReadOnlyList<UserFilter>> FindMatchingFiltersAsync(Listing listing, CancellationToken ct = default);
}

public interface INotificationDispatcher
{
    Task NotifyAsync(UserFilter filter, Listing listing, CancellationToken ct = default);
}

public sealed record RawListing(
    Guid SourceId,
    string ExternalId,
    string SourceUrl,
    string Text,
    string? AuthorName,
    string? AuthorProfileUrl,
    IReadOnlyList<string> ImageUrls,
    DateTime PostedAt);

public sealed record ListingSearchCriteria
{
    public Guid? LocationId { get; init; }
    public decimal? MaxPrice { get; init; }
    public decimal? MinPrice { get; init; }
    public IReadOnlyList<string>? Areas { get; init; }
    public IReadOnlyList<PropertyType>? PropertyTypes { get; init; }
    public bool? PetsAllowed { get; init; }
    public DateTime? PostedAfter { get; init; }
    public int Limit { get; init; } = 20;
}
