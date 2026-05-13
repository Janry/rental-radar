# HANDOFF — Phase 3: Source auto-discovery

> Phase 2 (SQLite persistence) is **DONE**. Brief below is for the next session.
> Read `CLAUDE.md` first for project-wide context.

## Where Phase 2 left things

- Schema is live: `locations`, `scrape_sources`, `listings`, `user_filters` (SQLite, snake_case).
- `LocationRepository` + `ScrapeSourceRepository` + `ListingRepository` + `UserFilterRepository` all wired.
- `StubSourceDiscoveryService` returns an empty list — that's the placeholder Phase 3 replaces.
- MCP tools `add_location` / `discover_sources` already call `ISourceDiscoveryService.DiscoverAsync()` — once a real implementation is wired, both immediately become useful end-to-end.

## 🎯 Goal of Phase 3

Implement `ISourceDiscoveryService` so that adding a new Location automatically finds candidate FB groups for it. After this phase, calling `add_location` for Ko Phangan should return ~5-15 ranked FB-group candidates with member counts and an AI-evaluated relevance score.

## Sketch of approach (to be refined when phase starts)

1. **Playwright search** — open Facebook search UI for each keyword from `Location.SearchKeywords`, scrape the "Groups" tab results. Returns group URL, name, member count.
2. **De-dup + filter** — merge results across keywords, drop groups already in `scrape_sources`, drop those with `<N` members.
3. **AI ranking** — for each candidate, ask Claude API: *"This group is called X with Y members. Is it about long-term rental in {location.Name}? Score 0-1."*
4. **Return ranked candidates** — `IReadOnlyList<ScrapeSource>` with `RelevanceScore` set. Persistence is up to MCP tool (`add_location` already saves the Location; sources get saved when user approves via a future `enable_source` MCP tool — or auto-enable top-K candidates).

## Open questions for that phase

- FB scraper auth: throwaway account stored in `.env`? Or Playwright session JSON?
- Rate-limit strategy: randomized delays per FB request, single-process discovery, no parallel queries per IP.
- Where to put Playwright bootstrap (browser install) — Phase 3 first run? Docker image build step? Both?
- Reuse of FB session cookie between discovery and scraper Worker (Phase 4) — single shared `IFacebookSession`?

## Definition of Done

- `ISourceDiscoveryService` produces non-empty results for at least one real location (Ko Phangan)
- `discover_sources` MCP tool returns ranked candidates in Claude Desktop
- New service registered in `RR.Infrastructure.DependencyInjection` (replacing `StubSourceDiscoveryService`)
- Integration test or smoke harness that proves the Playwright path works locally (mocking FB API is impossible — at minimum, a documented manual test in `docs/`)
