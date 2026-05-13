namespace RR.Scraper.Worker;

public sealed class ScrapingOptions
{
    public const string SectionName = "Scraping";

    public int IntervalMinutes { get; init; } = 15;
    public int ListingRetentionDays { get; init; } = 30;
    public int MaxConsecutiveFailures { get; init; } = 5;
    public int MinDelayBetweenSourcesSec { get; init; } = 15;
    public int MaxDelayBetweenSourcesSec { get; init; } = 45;
}
