using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RR.Core.Domain;
using RR.Infrastructure.Persistence;
using RR.Infrastructure.Persistence.Repositories;
using Xunit;

namespace RR.IntegrationTests;

public sealed class LocationRepositoryTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"rr_test_{Guid.NewGuid():N}.db");
    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .UseSnakeCaseNamingConvention()
            .Options;

        _db = new AppDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        // SQLite тримає файл відкритим через connection pool; явно його очищаємо
        // перш ніж видаляти файл, інакше File.Delete падає з IOException.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task Add_and_fetch_by_slug_roundtrips_all_fields()
    {
        var repo = new LocationRepository(_db);

        var location = new Location
        {
            Name = "Ko Phangan",
            Slug = "ko-phangan",
            Country = "TH",
            Currency = "THB",
            Timezone = "Asia/Bangkok",
            Areas = ["Sri Thanu", "Tong Sala", "Haad Rin"],
            SearchKeywords = ["Ko Phangan rent", "Sri Thanu rent"]
        };

        await repo.AddAsync(location);

        var fetched = await repo.GetBySlugAsync("ko-phangan");

        Assert.NotNull(fetched);
        Assert.Equal(location.Id, fetched.Id);
        Assert.Equal("Ko Phangan", fetched.Name);
        Assert.Equal("TH", fetched.Country);
        Assert.Equal("THB", fetched.Currency);
        Assert.Equal("Asia/Bangkok", fetched.Timezone);
        Assert.Equal(3, fetched.Areas.Count);
        Assert.Contains("Sri Thanu", fetched.Areas);
        Assert.Equal(2, fetched.SearchKeywords.Count);
    }

    [Fact]
    public async Task Unique_slug_constraint_prevents_duplicates()
    {
        var repo = new LocationRepository(_db);

        await repo.AddAsync(new Location { Name = "Canggu", Slug = "canggu", Country = "ID" });

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            repo.AddAsync(new Location { Name = "Canggu Duplicate", Slug = "canggu", Country = "ID" }));
    }
}
