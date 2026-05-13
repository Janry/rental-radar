# RentalRadar — Project Context for Claude Code

> This file is auto-loaded by Claude Code at the start of every session.
> It contains the project's full context, conventions, and current state.

## 🎯 What is this project

**RentalRadar** is a location-agnostic AI-powered rental listings aggregator. It scrapes Facebook groups & marketplaces for any city/island/neighborhood the user adds, structures unstructured posts with Claude AI, and sends real-time Telegram notifications when listings match user filters.

The project is being built as a **production-grade portfolio piece** demonstrating:
- MCP (Model Context Protocol) server development in C# — a rare, in-demand skill
- Clean Architecture in .NET 9
- AI-augmented data pipelines (Claude API for unstructured → structured extraction)
- Real-world web scraping (Playwright, Facebook)
- Docker, CI/CD, integration tests with Testcontainers
- Free-tier production deployment (Oracle Cloud Always Free)

The user (developer) lives in Thailand and uses Ko Phangan as the seed location, but the system is built to handle any location added at runtime.

## 🏗️ Architecture overview

```
Location-agnostic core:
  Location → discovers → ScrapeSource(s) → produces → Listing → matches → UserFilter → notifies → Telegram

Components:
  Scraper Worker (Playwright)  ──┐
                                  ├──▶ PostgreSQL ◀──── MCP Server (Claude Desktop)
  AI Extractor (Claude API)    ──┤                    
                                  │
  Matching Engine ──▶ Telegram Bot Notifier
```

## 📐 Clean Architecture layers

The solution is `RentalRadar.sln` with 6 projects under `src/` and 2 under `tests/`:

| Project | Purpose | Dependencies |
|---------|---------|--------------|
| `RR.Core` | Domain entities + interfaces. **Zero dependencies.** | (none) |
| `RR.Infrastructure` | EF Core, repositories, Facebook scraper, Claude client | RR.Core |
| `RR.McpServer` | MCP server exposing tools to Claude Desktop | RR.Core, RR.Infrastructure |
| `RR.Scraper.Worker` | BackgroundService running scraping on schedule | RR.Core, RR.Infrastructure |
| `RR.TelegramBot` | Notification dispatcher + interactive bot | RR.Core, RR.Infrastructure |
| `RR.AiFilter` | Claude-powered extraction & semantic matching | RR.Core, RR.Infrastructure |

**Dependency rule:** anything can depend on `RR.Core`. Only `Infrastructure` and host projects depend on external libs. **Never** put EF Core, HTTP clients, or any framework code in `RR.Core`.

## 🧩 Domain model (in RR.Core/Domain/)

### `Location` — the root entity
- A monitored place: city/island/neighborhood
- Holds: Name, Slug, Country, Currency, Timezone, Areas[], SearchKeywords[], Sources[]
- Added at runtime via MCP `add_location` tool, not hardcoded
- Each Location has its own currency — `Listing.PricePerMonth` is in Location's currency

### `ScrapeSource` — where data comes from
- FB group, marketplace category, or future sources (Telegram, Craigslist…)
- Belongs to one Location (LocationId)
- Discovered automatically by `ISourceDiscoveryService` or added manually
- Has RelevanceScore (AI-evaluated 0.0-1.0) and health metrics

### `Listing` — a single rental post
- Linked to Location and ScrapeSource by ID
- `ExternalId` is unique within a source (used for dedup)
- Raw text + AI-extracted structured fields (price, area, bedrooms, amenities)
- `ConfidenceScore` shows how sure AI is this is actually a rental

### `UserFilter` — notification subscription
- Belongs to one TelegramChatId + one Location
- Structured criteria (max price, areas, property types) + optional `SemanticQuery` for AI matching
- Engine matches new Listings against active filters and pings the user

## 🔌 Key interfaces (in RR.Core/Abstractions/Interfaces.cs)

- `IListingRepository`, `IUserFilterRepository`, `ILocationRepository` — data access
- `ISourceDiscoveryService` — finds FB groups for a Location from keywords
- `IFacebookScraper` — Playwright-based scraping; returns `IAsyncEnumerable<RawListing>`
- `IAiListingExtractor` — `RawListing` → `Listing` via Claude API
- `IMatchingEngine` — finds filters that match a new listing
- `INotificationDispatcher` — sends Telegram notifications

All interfaces live in Core. Infrastructure provides implementations.

## 🛠️ Tech stack

- **.NET 9** — runtime
- **ModelContextProtocol** v0.3.0-preview.4 — official MCP SDK (Anthropic + Microsoft)
- **EF Core 9 + SQLite** — embedded file-based DB (`Microsoft.EntityFrameworkCore.Sqlite`)
- **EFCore.NamingConventions** — snake_case columns/tables
- **Microsoft.Playwright** 1.49 — headless Chromium for FB scraping
- **Anthropic.SDK** 5.0 — Claude API client
- **Telegram.Bot** — bot library (to be added in Phase 6)
- **Serilog + OpenTelemetry + Seq** — observability
- **xUnit** — integration tests (SQLite tempfile, no Testcontainers needed)
- **GitHub Actions** — CI/CD
- **Oracle Cloud Always Free** (4 ARM cores, 24 GB RAM) — hosting target

**DB choice — SQLite, not PostgreSQL.** Decided in Phase 2 after analysing actual data needs:
- Listings are short-lived (TTL ~30 days) — Telegram is the user-facing history
- Config (Locations, Sources, Filters) is tiny (десятки-сотні рядків)
- Three processes (scraper / bot / MCP) share data — SQLite WAL handles concurrency
- Zero infra (one file) vs. an extra Postgres container
- Provider swap to Postgres later = `UseSqlite()` → `UseNpgsql()` + regenerate migrations

## 🎨 Code conventions

- C# 12 / .NET 9 idioms: file-scoped namespaces, primary constructors, `required` members
- Nullable reference types **enabled** everywhere
- Domain entities use `init` for immutable IDs and `set` for mutable derived fields
- All async methods take `CancellationToken ct = default` and return `Task`
- MCP tools use `[McpServerTool]` + `[Description]` attributes (descriptions are how Claude understands when to call them — write them carefully)
- Logging via Serilog with structured properties: `Log.Information("Scraped {Count} listings from {Source}", count, source.Name)` — never string concatenation
- Repositories return `IReadOnlyList<T>`, never `List<T>` or `IEnumerable<T>`
- Domain code stays free of attributes from EF Core, JSON serialization, etc. — configure those in Infrastructure

## 🌍 Localization & language

- **Code, identifiers, English-facing strings, comments in shared code → English**
- **User-facing strings in MCP tool responses and Telegram messages → Ukrainian** (the user speaks Ukrainian and the Telegram bot is for them personally)
- **Internal explanatory comments in Ukrainian are fine** for personal clarity

## ⚖️ Legal & ethical constraints

- Scrape **publicly visible** content only — no bypassing auth walls
- No storing PII beyond what's already publicly visible in posts (author display name OK, full profiles not)
- Respect rate limits — randomized delays between requests, no parallelism per source
- This is a **personal-use** tool, not a commercial product
- Document everything in `docs/LEGAL.md` (to be created)

## 📊 Current progress

### ✅ Phase 1 — Core domain & MCP skeleton (DONE)
- Solution structure (`RentalRadar.slnx`)
- Domain: `Location`, `ScrapeSource`, `Listing`, `UserFilter`, `PropertyType`
- All interfaces in `RR.Core/Abstractions/Interfaces.cs`
- MCP server entry point (`Program.cs`) wired with stdio transport
- MCP tool classes registered: `LocationTools`, `SourceManagementTools` (Phase 3a), `RentalSearchTools`, `FilterManagementTools`
- 12 MCP tools defined: `add_location`, `list_locations`, `discover_sources` (stub), `add_source`, `list_sources`, `set_source_enabled`, `remove_source`, `search_rentals`, `get_listing_details`, `create_notification_filter`, `list_my_filters`, `delete_filter`

### ✅ Phase 2 — SQLite persistence (DONE)
- `AppDbContext` + 4 entity configurations, snake_case naming
- 4 repositories (`Location`, `ScrapeSource`, `Listing`, `UserFilter`) + `IScrapeSourceRepository` added to Core
- `StubSourceDiscoveryService` — empty until Phase 3
- `IListingRepository.DeleteOlderThanAsync()` — TTL-cleanup hook for Phase 4 scraper
- `appsettings.json` + Development overrides
- `InitialCreate` EF Core migration in `RR.Infrastructure/Persistence/Migrations/`
- `dotnet-ef` as local tool (`.config/dotnet-tools.json`)
- Integration test for `LocationRepository` (xUnit + temp SQLite file)
- `docs/MCP_SETUP.md`
- Postgres removed from `docker/docker-compose.yml`; shared volume for SQLite file

### ✅ Phase 3a — Manual source management (DONE)
`SourceManagementTools` MCP class з 4 tools (`add_source`, `list_sources`, `set_source_enabled`, `remove_source`). Дозволяє користувачу руками вставити FB-URL-и в Claude Desktop і отримати готову систему до Phase 4 (scraper). `ISourceDiscoveryService` seam — без змін, Phase 3b просто замінить stub.

### ⏭️ Phase 3b — Auto-discovery (DEFERRED, see HANDOFF.md)
Playwright-based FB пошук + Claude ranker. Відкладено доки manual flow не доведе value; auto-discovery — bell-and-whistle поверх робочого MVP.

### ⏳ Future phases (high-level)
- **Phase 4** — Facebook scraper implementation
- **Phase 5** — AI extraction pipeline (Claude API for parsing posts)
- **Phase 6** — Telegram bot + notification engine
- **Phase 7** — Docker images + Oracle Cloud deployment
- **Phase 8** — CI/CD polishing
- **Phase 9** — (optional) Web dashboard

## 🚦 Working agreements with the user

- The user is an **experienced developer** — don't over-explain basics
- The user writes Ukrainian by default; reply in Ukrainian unless code/identifiers are involved
- When making meaningful design decisions, **surface trade-offs explicitly** instead of just picking
- Ask before pivoting on architecture or adding new dependencies
- For new external deps, default to **mature, popular libraries** with active maintenance — no obscure packages
- Always run/build/test code that's written before declaring done
- When unsure between two approaches, present both with pros/cons rather than guess

## 📁 Useful commands

```bash
# First time on a fresh clone
dotnet tool restore           # installs dotnet-ef locally
dotnet build RentalRadar.slnx

# Run the MCP server locally (it talks via stdio — for direct invocation)
dotnet run --project src/RR.McpServer

# Run tests
dotnet test RentalRadar.slnx

# EF Core migrations
dotnet ef migrations add <Name> --project src/RR.Infrastructure --startup-project src/RR.McpServer --output-dir Persistence/Migrations
dotnet ef database update       --project src/RR.Infrastructure --startup-project src/RR.McpServer

# Docker stack (Phase 7+)
docker compose -f docker/docker-compose.yml up -d
```

## 📞 If you (Claude Code) get stuck

- Re-read this `CLAUDE.md` first
- Check `HANDOFF.md` for the current phase's detailed brief
- Domain types live in `src/RR.Core/Domain/` — read them before guessing field names
- Interfaces in `src/RR.Core/Abstractions/Interfaces.cs` are the contract; respect it
- If a design choice isn't covered here, **ask the user** rather than improvise
