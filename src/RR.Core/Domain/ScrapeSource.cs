namespace RR.Core.Domain;

/// <summary>
/// Джерело даних, з якого скрапер витягує оголошення.
/// Прив'язане до конкретної Location.
/// </summary>
public sealed class ScrapeSource
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid LocationId { get; init; }

    public required string Url { get; init; }
    public required string Name { get; init; }              // "Ko Phangan Rentals" (від FB)
    public required ScrapeSourceType Type { get; init; }

    /// <summary>Скільки членів у групі / активність — для ранжування.</summary>
    public int? MemberCount { get; set; }

    /// <summary>0.0-1.0 — наскільки джерело релевантне для цієї локації (оцінює AI).</summary>
    public double RelevanceScore { get; set; } = 1.0;

    public bool IsAutoDiscovered { get; init; }
    public bool IsEnabled { get; set; } = true;

    public DateTime AddedAt { get; init; } = DateTime.UtcNow;
    public DateTime? LastScrapedAt { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public int ConsecutiveFailures { get; set; }
}

public enum ScrapeSourceType
{
    FacebookGroup,
    FacebookMarketplace,
    // Готово на майбутнє — щоб не лочитись лише на FB
    Telegram,
    Craigslist,
    Custom
}
