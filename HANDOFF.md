# HANDOFF — Phase 4: Facebook scraper

> Phase 3a (manual source management) is **DONE**.
> Phase 3b (auto-discovery via Playwright + AI ranker) is **DEFERRED** until Phase 4+ proves end-to-end value.
> Read `CLAUDE.md` first for project-wide context.

## Where things stand

- Persistence layer alive (Phase 2): SQLite, EF Core, repos.
- User can manually add FB-group URLs via Claude Desktop using `add_source` MCP tool (Phase 3a).
- `ISourceDiscoveryService` is still a stub — replacing it is what Phase 3b would do, but we deferred until the scraper actually works.
- Domain has `ScrapeSource.IsEnabled`, `LastScrapedAt`, `LastSuccessAt`, `ConsecutiveFailures` — scraper Worker will write to these.

## 🎯 Goal of Phase 4

Implement `IFacebookScraper` so a background Worker can periodically visit each enabled `ScrapeSource`, pull recent posts, hand them to AI extraction (Phase 5), and persist the resulting `Listing` rows.

After this phase: scraper Worker runs every N minutes, new posts arrive in the DB and (eventually) ping the user via Telegram.

## Concrete deliverables

1. **`RR.Scraper.Worker` project** — its csproj doesn't exist yet (declared in `.slnx` Phase 1 but never created). Create it as a .NET 9 Worker Service.
2. **`FacebookScraper : IFacebookScraper`** in `RR.Infrastructure/Scraping/` — Playwright headless, login via session JSON, iterates posts in a group, yields `RawListing` records.
3. **Session bootstrap** — separate helper `dotnet run --project tools/FbLogin -- <output-session.json>` that opens Chromium non-headless for manual login, then dumps cookies. Session JSON path goes into config.
4. **Worker BackgroundService** — loop: get all `IsEnabled` sources → for each, call `IFacebookScraper.ScrapeAsync` → write `RawListing` to a queue (or directly to listings with stub extractor until Phase 5 implements real one).
5. **Update `ScrapeSource.LastScrapedAt`, `ConsecutiveFailures`** after each pass.
6. **Listing TTL cleanup** call (`IListingRepository.DeleteOlderThanAsync`) on Worker startup using `LISTING_RETENTION_DAYS` from config.
7. **Docker** — Worker container needs Chromium + Playwright deps; use `mcr.microsoft.com/playwright/dotnet:v1.49.0-jammy` as base image.

## Open questions

- **Where does Playwright browser binary live?** First-run download (`playwright install chromium`) on Worker start, or baked into Docker image? I'd say: baked into the playwright/dotnet base image (zero runtime overhead), separately a `playwright install` step in dev setup docs.
- **What's the post-iteration limit?** Hard-code "last 24h" first; configurable later.
- **What happens if FB session expires?** Worker should log error, mark consecutive failure, NOT crash. Operator re-runs login tool.
- **Phase 5 boundary** — do we keep AI extraction inside the scraper Worker process, or a separate service consuming a queue? Probably same process for simplicity until volume justifies splitting.

## Phase 3b (auto-discovery) — when to come back

Pick up Phase 3b once Phase 4-5 produce real listings. By then we'll know:
- Whether manual source management is enough (probably for stable user with <5 locations)
- Whether FB anti-bot heuristics break the scraper anyway (in which case auto-discovery would face same wall)
- What "good" relevance looks like — informing the AI ranker prompt

Sketch is preserved in git history (`HANDOFF.md` before Phase 3a commit) if needed.
