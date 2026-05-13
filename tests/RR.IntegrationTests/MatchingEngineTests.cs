using System.Text.Json.Nodes;
using Anthropic.SDK.Messaging;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RR.Core.Domain;
using RR.Infrastructure.Ai;
using RR.Infrastructure.Matching;
using RR.Infrastructure.Persistence;
using RR.Infrastructure.Persistence.Repositories;
using Xunit;

namespace RR.IntegrationTests;

public sealed class MatchingEngineTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"rr_match_{Guid.NewGuid():N}.db");
    private AppDbContext _db = null!;
    private UserFilterRepository _filterRepo = null!;
    private Location _location = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .UseSnakeCaseNamingConvention()
            .Options;
        _db = new AppDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        _location = new Location
        {
            Name = "Ko Phangan", Slug = "ko-phangan", Country = "TH", Currency = "THB",
            Areas = ["Sri Thanu", "Tong Sala"]
        };
        _db.Locations.Add(_location);
        await _db.SaveChangesAsync();

        _filterRepo = new UserFilterRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private MatchingEngine BuildEngine(IClaudeClient? claude = null) => new(
        _filterRepo,
        claude ?? new FakeClaudeClient(_ => throw new InvalidOperationException("Claude should not be called")),
        Options.Create(new AnthropicOptions()),
        NullLogger<MatchingEngine>.Instance);

    private Listing TestListing(
        decimal? price = 12000m, string? area = "Sri Thanu", int? bedrooms = 1,
        PropertyType? type = PropertyType.Studio, bool? pets = null) =>
        new()
        {
            LocationId = _location.Id,
            SourceId = Guid.NewGuid(),
            ExternalId = "ext-1",
            SourceUrl = "https://facebook.com/posts/x",
            RawText = "Nice studio in Sri Thanu",
            PricePerMonth = price,
            Area = area,
            Bedrooms = bedrooms,
            PropertyType = type,
            PetsAllowed = pets,
            PostedAt = DateTime.UtcNow
        };

    [Fact]
    public async Task Price_above_max_excludes_filter()
    {
        await _filterRepo.AddAsync(new UserFilter
        {
            TelegramChatId = 1, LocationId = _location.Id, Name = "Cheap", MaxPrice = 10000m
        });
        var engine = BuildEngine();

        var matches = await engine.FindMatchingFiltersAsync(TestListing(price: 15000m));

        Assert.Empty(matches);
    }

    [Fact]
    public async Task Price_within_range_matches()
    {
        await _filterRepo.AddAsync(new UserFilter
        {
            TelegramChatId = 1, LocationId = _location.Id, Name = "Range",
            MinPrice = 10000m, MaxPrice = 20000m
        });
        var engine = BuildEngine();

        var matches = await engine.FindMatchingFiltersAsync(TestListing(price: 15000m));

        Assert.Single(matches);
    }

    [Fact]
    public async Task Area_mismatch_excludes_filter()
    {
        await _filterRepo.AddAsync(new UserFilter
        {
            TelegramChatId = 1, LocationId = _location.Id, Name = "TS-only", Areas = ["Tong Sala"]
        });
        var engine = BuildEngine();

        var matches = await engine.FindMatchingFiltersAsync(TestListing(area: "Sri Thanu"));

        Assert.Empty(matches);
    }

    [Fact]
    public async Task Property_type_filter_works()
    {
        await _filterRepo.AddAsync(new UserFilter
        {
            TelegramChatId = 1, LocationId = _location.Id, Name = "Bungalow only",
            PropertyTypes = [PropertyType.Bungalow]
        });
        var engine = BuildEngine();

        var studio = await engine.FindMatchingFiltersAsync(TestListing(type: PropertyType.Studio));
        var bungalow = await engine.FindMatchingFiltersAsync(TestListing(type: PropertyType.Bungalow));

        Assert.Empty(studio);
        Assert.Single(bungalow);
    }

    [Fact]
    public async Task Require_pets_allowed_filters_correctly()
    {
        await _filterRepo.AddAsync(new UserFilter
        {
            TelegramChatId = 1, LocationId = _location.Id, Name = "Pet ok",
            RequirePetsAllowed = true
        });
        var engine = BuildEngine();

        Assert.Empty(await engine.FindMatchingFiltersAsync(TestListing(pets: false)));
        Assert.Empty(await engine.FindMatchingFiltersAsync(TestListing(pets: null)));   // unknown = not allowed
        Assert.Single(await engine.FindMatchingFiltersAsync(TestListing(pets: true)));
    }

    [Fact]
    public async Task Filter_without_semantic_query_skips_claude()
    {
        await _filterRepo.AddAsync(new UserFilter
        {
            TelegramChatId = 1, LocationId = _location.Id, Name = "Plain"
        });
        var engine = BuildEngine();   // FakeClaudeClient throws if called

        var matches = await engine.FindMatchingFiltersAsync(TestListing());
        Assert.Single(matches);
    }

    [Fact]
    public async Task Semantic_match_returns_filter_only_if_claude_says_match()
    {
        await _filterRepo.AddAsync(new UserFilter
        {
            TelegramChatId = 1, LocationId = _location.Id, Name = "Quiet",
            SemanticQuery = "тихе місце поруч з джунглями"
        });

        var matchingClaude = new FakeClaudeClient(_ => SemanticResponse(matches: true));
        var notMatchingClaude = new FakeClaudeClient(_ => SemanticResponse(matches: false));

        Assert.Single(await BuildEngine(matchingClaude).FindMatchingFiltersAsync(TestListing()));
        Assert.Empty(await BuildEngine(notMatchingClaude).FindMatchingFiltersAsync(TestListing()));
    }

    [Fact]
    public async Task Semantic_match_falls_back_to_structural_on_claude_error()
    {
        await _filterRepo.AddAsync(new UserFilter
        {
            TelegramChatId = 1, LocationId = _location.Id, Name = "Quiet",
            SemanticQuery = "тихе місце"
        });
        var brokenClaude = new FakeClaudeClient(_ => throw new HttpRequestException("rate limit"));
        var engine = BuildEngine(brokenClaude);

        var matches = await engine.FindMatchingFiltersAsync(TestListing());

        Assert.Single(matches);   // structural passed → falls back to "match"
    }

    private static MessageResponse SemanticResponse(bool matches) => new()
    {
        Content = new List<ContentBase>
        {
            new ToolUseContent
            {
                Id = "tu1",
                Name = "evaluate_semantic_match",
                Input = JsonNode.Parse($$"""{ "matches": {{matches.ToString().ToLowerInvariant()}}, "reason": "test" }""")!
            }
        }
    };
}
