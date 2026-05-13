using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RR.Core.Domain;
using RR.Infrastructure.Persistence;
using RR.Infrastructure.Persistence.Repositories;
using Xunit;

namespace RR.IntegrationTests;

public sealed class ScrapeSourceRepositoryTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"rr_test_{Guid.NewGuid():N}.db");
    private AppDbContext _db = null!;
    private Location _location = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .UseSnakeCaseNamingConvention()
            .Options;

        _db = new AppDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        _location = new Location { Name = "Ko Phangan", Slug = "ko-phangan", Country = "TH" };
        _db.Locations.Add(_location);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task AddRange_then_filter_by_location_returns_enabled_and_disabled()
    {
        var repo = new ScrapeSourceRepository(_db);

        await repo.AddRangeAsync(
        [
            new ScrapeSource { LocationId = _location.Id, Url = "https://facebook.com/groups/a", Name = "A", Type = ScrapeSourceType.FacebookGroup, IsEnabled = true },
            new ScrapeSource { LocationId = _location.Id, Url = "https://facebook.com/groups/b", Name = "B", Type = ScrapeSourceType.FacebookGroup, IsEnabled = false }
        ]);

        var byLocation = await repo.GetByLocationAsync(_location.Id);
        var enabled = await repo.GetEnabledAsync();

        Assert.Equal(2, byLocation.Count);
        Assert.Single(enabled);
        Assert.Equal("A", enabled[0].Name);
    }

    [Fact]
    public async Task Unique_url_per_location_constraint_is_enforced()
    {
        var repo = new ScrapeSourceRepository(_db);

        await repo.AddAsync(new ScrapeSource
        {
            LocationId = _location.Id,
            Url = "https://facebook.com/groups/koh-phangan-rentals",
            Name = "KP Rentals",
            Type = ScrapeSourceType.FacebookGroup
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => repo.AddAsync(new ScrapeSource
        {
            LocationId = _location.Id,
            Url = "https://facebook.com/groups/koh-phangan-rentals",
            Name = "Duplicate",
            Type = ScrapeSourceType.FacebookGroup
        }));
    }

    [Fact]
    public async Task Delete_removes_the_source()
    {
        var repo = new ScrapeSourceRepository(_db);

        var source = new ScrapeSource
        {
            LocationId = _location.Id,
            Url = "https://facebook.com/groups/to-delete",
            Name = "ToDelete",
            Type = ScrapeSourceType.FacebookGroup
        };
        await repo.AddAsync(source);

        await repo.DeleteAsync(source.Id);

        var fetched = await repo.GetByIdAsync(source.Id);
        Assert.Null(fetched);
    }
}
