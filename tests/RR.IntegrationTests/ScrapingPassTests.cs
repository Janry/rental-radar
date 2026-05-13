using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RR.Core.Abstractions;
using RR.Core.Domain;
using RR.Infrastructure.Ai;
using RR.Infrastructure.Persistence;
using RR.Infrastructure.Persistence.Repositories;
using RR.Scraper.Worker;
using Xunit;

namespace RR.IntegrationTests;

public sealed class ScrapingPassTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"rr_pass_{Guid.NewGuid():N}.db");
    private ServiceProvider _sp = null!;
    private FakeFacebookScraper _scraper = null!;
    private Location _location = null!;
    private ScrapeSource _source = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<AppDbContext>(opts =>
            opts.UseSqlite($"Data Source={_dbPath}").UseSnakeCaseNamingConvention());

        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IScrapeSourceRepository, ScrapeSourceRepository>();
        services.AddScoped<IListingRepository, ListingRepository>();
        services.AddScoped<IAiListingExtractor, StubAiListingExtractor>();

        _scraper = new FakeFacebookScraper();
        services.AddSingleton<IFacebookScraper>(_scraper);

        // Дуже короткі затримки — тести не повинні чекати реальні 15-45с.
        services.AddSingleton<IOptions<ScrapingOptions>>(Options.Create(new ScrapingOptions
        {
            MinDelayBetweenSourcesSec = 0,
            MaxDelayBetweenSourcesSec = 0,
            MaxConsecutiveFailures = 3
        }));

        services.AddSingleton<ScrapingPass>();

        _sp = services.BuildServiceProvider();

        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        _location = new Location { Name = "Ko Phangan", Slug = "ko-phangan", Country = "TH" };
        _source = new ScrapeSource
        {
            LocationId = _location.Id,
            Url = "https://facebook.com/groups/kp-rentals",
            Name = "KP Rentals",
            Type = ScrapeSourceType.FacebookGroup,
            IsEnabled = true
        };
        db.Locations.Add(_location);
        db.ScrapeSources.Add(_source);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _sp.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task First_pass_adds_listings_then_second_pass_dedupes_them()
    {
        _scraper.Behaviour = (_) => AsyncEnumerable(
            MakeRaw(_source.Id, "post-1", "Studio in Sri Thanu, 12k baht/mo"),
            MakeRaw(_source.Id, "post-2", "Bungalow Tong Sala, 18k"));

        var pass = _sp.GetRequiredService<ScrapingPass>();

        var s1 = await pass.RunAsync();
        Assert.Equal(2, s1.TotalAdded);
        Assert.Equal(0, s1.TotalSkipped);

        var s2 = await pass.RunAsync();
        Assert.Equal(0, s2.TotalAdded);
        Assert.Equal(2, s2.TotalSkipped);

        await using var verify = _sp.CreateAsyncScope();
        var listings = verify.ServiceProvider.GetRequiredService<AppDbContext>().Listings;
        Assert.Equal(2, await listings.CountAsync());
    }

    [Fact]
    public async Task Exception_increments_consecutive_failures_and_auto_disables_at_threshold()
    {
        _scraper.Behaviour = (_) => ThrowAsync<RawListing>(new InvalidOperationException("session expired"));

        var pass = _sp.GetRequiredService<ScrapingPass>();

        await pass.RunAsync();
        Assert.Equal(1, await GetConsecutiveFailuresAsync());
        Assert.True(await GetIsEnabledAsync());

        await pass.RunAsync();
        Assert.Equal(2, await GetConsecutiveFailuresAsync());
        Assert.True(await GetIsEnabledAsync());

        await pass.RunAsync();
        Assert.Equal(3, await GetConsecutiveFailuresAsync());
        Assert.False(await GetIsEnabledAsync());   // auto-disabled at MaxConsecutiveFailures=3
    }

    [Fact]
    public async Task Successful_pass_after_failures_resets_counter()
    {
        _scraper.Behaviour = (_) => ThrowAsync<RawListing>(new InvalidOperationException("transient"));
        var pass = _sp.GetRequiredService<ScrapingPass>();
        await pass.RunAsync();
        await pass.RunAsync();
        Assert.Equal(2, await GetConsecutiveFailuresAsync());

        _scraper.Behaviour = (_) => AsyncEnumerable(MakeRaw(_source.Id, "p1", "post"));
        await pass.RunAsync();

        Assert.Equal(0, await GetConsecutiveFailuresAsync());
    }

    private async Task<int> GetConsecutiveFailuresAsync()
    {
        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.ScrapeSources.AsNoTracking().FirstAsync(s => s.Id == _source.Id))
            .ConsecutiveFailures;
    }

    private async Task<bool> GetIsEnabledAsync()
    {
        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.ScrapeSources.AsNoTracking().FirstAsync(s => s.Id == _source.Id))
            .IsEnabled;
    }

    private static RawListing MakeRaw(Guid sourceId, string externalId, string text) =>
        new(sourceId, externalId, $"https://facebook.com/posts/{externalId}", text,
            "Test Author", null, Array.Empty<string>(), DateTime.UtcNow);

    private static async IAsyncEnumerable<T> AsyncEnumerable<T>(params T[] items)
    {
        foreach (var item in items) { yield return item; await Task.Yield(); }
    }

    private static async IAsyncEnumerable<T> ThrowAsync<T>(Exception ex)
    {
        await Task.Yield();
        throw ex;
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}

internal sealed class FakeFacebookScraper : IFacebookScraper
{
    public Func<ScrapeSource, IAsyncEnumerable<RawListing>> Behaviour { get; set; } =
        (_) => Empty();

    public IAsyncEnumerable<RawListing> ScrapeAsync(ScrapeSource source, CancellationToken ct = default) =>
        Behaviour(source);

    private static async IAsyncEnumerable<RawListing> Empty()
    {
        await Task.Yield();
        yield break;
    }
}
