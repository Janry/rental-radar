# HANDOFF — Phase 2: PostgreSQL + EF Core

> Read `CLAUDE.md` first for project-wide context. This file is the detailed brief for the next phase only.

## 🎯 Goal of this phase

Get the MCP server **fully working with a real PostgreSQL database**. After this phase:
- The user can launch the stack with `docker compose up postgres -d`
- Run the MCP server, connect Claude Desktop to it
- Call `add_location` from a Claude Desktop chat and see the row appear in PG
- Call `list_locations`, `create_notification_filter`, etc. — all backed by real data
- No scraping or AI yet — those come in Phases 3-5

This is the milestone that turns the project from "scaffold" into "alive".

## 📋 Concrete deliverables

### 1. `AppDbContext` in `RR.Infrastructure/Persistence/AppDbContext.cs`
- Inherits `DbContext`
- DbSets for: `Locations`, `ScrapeSources`, `Listings`, `UserFilters`
- Constructor takes `DbContextOptions<AppDbContext>`
- Applies all configurations from the assembly via `modelBuilder.ApplyConfigurationsFromAssembly(...)`

### 2. EF Core entity configurations in `RR.Infrastructure/Persistence/Configurations/`
One file per entity, each implementing `IEntityTypeConfiguration<T>`:

- **`LocationConfiguration`**
  - Table: `locations`, PK: `Id`
  - Unique index on `Slug`
  - `Areas` and `SearchKeywords` → store as `text[]` (Npgsql native array support)
  - One-to-many: Location → ScrapeSources (cascade delete)

- **`ScrapeSourceConfiguration`**
  - Table: `scrape_sources`, PK: `Id`, FK to `locations(id)`
  - Unique index on `(LocationId, Url)`
  - Enum `Type` stored as string for readability

- **`ListingConfiguration`**
  - Table: `listings`, PK: `Id`
  - FK to `locations(id)` and `scrape_sources(id)`
  - **Critical:** unique index on `(SourceId, ExternalId)` — this is the dedup key
  - Index on `(LocationId, PostedAt DESC)` for fast "recent listings" queries
  - `ImageUrls`, `ContactInfo` → `text[]`
  - `PropertyType` enum → string

- **`UserFilterConfiguration`**
  - Table: `user_filters`, PK: `Id`, FK to `locations(id)`
  - Index on `TelegramChatId` (for listing user's filters)
  - Index on `(LocationId, IsActive)` for matching engine queries
  - `Areas`, `PropertyTypes` → `text[]`

Use **snake_case** column names (`UseSnakeCaseNamingConvention()` on options OR `[Column(Name=...)]` — pick one and be consistent).

### 3. Repository implementations in `RR.Infrastructure/Persistence/Repositories/`
One file per repo, each implementing the corresponding interface from `RR.Core.Abstractions`:

- `LocationRepository : ILocationRepository`
- `ScrapeSourceRepository` — **new interface needed**, add to Core
- `ListingRepository : IListingRepository`
- `UserFilterRepository : IUserFilterRepository`

**Implementation guidance:**
- Inject `AppDbContext` via primary constructor
- Use `AsNoTracking()` for read-only queries
- For `SearchAsync` in `ListingRepository`: build the `IQueryable` conditionally — only `.Where()` on fields the criteria specifies. Avoid `1=1` patterns.
- Return `IReadOnlyList<T>` via `.ToListAsync(ct)`

### 4. Wire it up in `RR.Infrastructure/DependencyInjection.cs`
Replace the empty stub with:
```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
{
    var connStr = cfg.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default not configured");

    services.AddDbContext<AppDbContext>(opts =>
        opts.UseNpgsql(connStr).UseSnakeCaseNamingConvention());

    services.AddScoped<ILocationRepository, LocationRepository>();
    services.AddScoped<IListingRepository, ListingRepository>();
    services.AddScoped<IUserFilterRepository, UserFilterRepository>();
    // ScrapeSource repo when interface added

    // Phase 3+: scraper, AI extractor, discovery service
    return services;
}
```

**Add NuGet packages to `RR.Infrastructure.csproj`:**
- `EFCore.NamingConventions` (for snake_case)
- Verify `Npgsql.EntityFrameworkCore.PostgreSQL` is already there

### 5. Initial EF Core migration
```bash
dotnet tool install --global dotnet-ef  # if not installed
dotnet ef migrations add InitialCreate \
    --project src/RR.Infrastructure \
    --startup-project src/RR.McpServer \
    --output-dir Persistence/Migrations
```

The migration file goes into source control. Don't apply it at startup automatically in MCP server — the user runs `dotnet ef database update` manually for the first time. (Auto-apply on startup can be added later behind a config flag.)

### 6. Configuration file for the MCP server
Create `src/RR.McpServer/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=rental_radar;Username=rr;Password=changeme"
  },
  "Serilog": {
    "MinimumLevel": "Information"
  }
}
```

And `appsettings.Development.json` for local overrides.

Update `Program.cs` to load configuration:
```csharp
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables();
```

### 7. Verify end-to-end manually
1. `docker compose -f docker/docker-compose.yml up postgres -d`
2. `dotnet ef database update --project src/RR.Infrastructure --startup-project src/RR.McpServer`
3. Connect Claude Desktop to the MCP server (instructions in `docs/MCP_SETUP.md` — **create this doc as part of this phase**)
4. In Claude Desktop, ask: *"Add Ko Phangan to RentalRadar, country TH, areas Sri Thanu and Tong Sala"*
5. Verify a row in `locations` table via `psql` or pgAdmin
6. Ask Claude: *"List my locations"* → it should return the one just added

## ⚠️ Watch out for

- **Don't auto-run migrations on MCP server startup** — MCP servers should start fast and stdio-clean. Migrations are a one-time op.
- **MCP servers communicate via stdio** — any `Console.WriteLine` corrupts the JSON-RPC protocol. All logs MUST go to stderr (Serilog config in `Program.cs` already does this — don't break it).
- The `ISourceDiscoveryService` is referenced in `LocationTools` constructor but has no implementation yet. For this phase, register a **stub implementation** that returns an empty list. Real discovery is Phase 3.
- `Listing.PropertyType` is named the same as the enum — EF Core can get confused. Use fully qualified names or rename the property to `Type` if needed.
- Don't forget to add `services.AddScoped<ISourceDiscoveryService, StubSourceDiscoveryService>()` so DI works.

## 🧪 Testing for this phase

Don't write unit tests for repositories (they'd just mock EF Core, low value). Instead, add **one integration test** in `RR.IntegrationTests` using Testcontainers:

```csharp
[Fact]
public async Task Can_create_and_retrieve_location()
{
    await using var container = new PostgreSqlBuilder().Build();
    await container.StartAsync();
    // ... wire up AppDbContext against container connection string
    // ... add a location, fetch by slug, assert equality
}
```

This proves the EF Core mapping works against real Postgres in CI.

## ✅ Definition of Done for Phase 2

- [ ] `dotnet build` succeeds with zero warnings
- [ ] `dotnet ef migrations add InitialCreate` produces a clean migration
- [ ] `dotnet ef database update` creates the schema in a local PG container
- [ ] MCP server starts without errors
- [ ] `add_location` call from Claude Desktop persists to DB
- [ ] `list_locations` reads it back correctly
- [ ] One Testcontainers-based integration test passes
- [ ] `docs/MCP_SETUP.md` exists with concrete steps to connect Claude Desktop
- [ ] All new code follows conventions in `CLAUDE.md` (snake_case in DB, English in code, Ukrainian in user-facing strings)

## 🔜 What comes after this

Phase 3 — Source auto-discovery. The plan: implement `ISourceDiscoveryService` using Playwright to search Facebook by keywords from a Location, scrape the search results page, rank candidates with Claude API, return for user approval. That's where the real fun starts.
