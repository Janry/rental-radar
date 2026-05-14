# 📡 RentalRadar

> **AI-powered rental listings radar — works for any city, island, or neighborhood.**
> Scrapes Facebook groups & marketplaces, structures messy posts with Claude AI,
> and pings you on Telegram the moment a matching listing appears.

[![.NET 9](https://img.shields.io/badge/.NET-9.0-purple)](https://dotnet.microsoft.com)
[![MCP](https://img.shields.io/badge/MCP-0.3-blue)](https://modelcontextprotocol.io)
[![Docker](https://img.shields.io/badge/Docker-ready-blue)](https://docker.com)
[![CI](https://github.com/Janry/rental-radar/actions/workflows/ci.yml/badge.svg)](https://github.com/Janry/rental-radar/actions)
[![codecov](https://codecov.io/gh/Janry/rental-radar/graph/badge.svg)](https://codecov.io/gh/Janry/rental-radar)

---

## 💡 The problem

Finding long-term rentals in places like Ko Phangan, Canggu, Tbilisi, or Lisbon is painful: **most listings live in scattered Facebook groups**, drowning in spam, written in inconsistent formats. You scroll for hours, miss the good ones, and end up paying broker fees.

**RentalRadar fixes this** for *any* location. Tell it "I'm moving to Chiang Mai", and it:
1. **Auto-discovers** relevant FB groups & marketplace categories by name
2. **Continuously scrapes** new posts (every 15 min)
3. **Extracts structured data** with Claude AI — price, area, amenities, dates
4. **Pings you on Telegram** only when something matches your filters
5. **Talks to you in Claude Desktop** through the MCP server

## 🗺️ Location-agnostic by design

You're not locked into one city. Add as many as you need:

```
add_location("Ko Phangan", country="TH", areas=["Sri Thanu", "Tong Sala"])
  → auto-finds 8 FB groups
add_location("Canggu", country="ID", areas=["Berawa", "Pererenan"])
  → auto-finds 5 FB groups
add_location("Lisbon", country="PT", areas=["Alfama", "Chiado"])
  → auto-finds 6 FB groups
```

Each location has its own currency, timezone, areas, and source pool.

## 🏗️ Architecture

```
┌────────────────────────────────────────────────────────────┐
│                    Location-agnostic core                   │
│  Location → discovers → ScrapeSource(s) → produces → Listing│
└────────────────────────────────────────────────────────────┘

  ┌────────────────┐    ┌───────────────────┐    ┌──────────────┐
  │ Scraper Worker │───▶│  PostgreSQL +     │◀───│ MCP Server   │
  │  (Playwright)  │    │  AI-structured    │    │ (Claude tool)│
  └───────┬────────┘    │  listings         │    └──────────────┘
          │             └────────┬──────────┘
          ▼                      ▲
  ┌────────────────┐    ┌────────┴──────────┐
  │  AI Extractor  │───▶│ Matching Engine    │
  │  (Claude API)  │    │ (per-Location)     │
  └────────────────┘    └────────┬──────────┘
                                 │
                                 ▼
                       ┌────────────────────┐
                       │  Telegram Bot      │
                       │  notifications     │
                       └────────────────────┘
```

## ✨ Features

- 🗺️ **Multi-location** — one instance monitors any number of cities/islands
- 🔎 **Source auto-discovery** — finds FB groups for new locations from name + areas
- 🤖 **AI extraction** — Claude API turns unstructured posts into structured listings
- 🧠 **Semantic filtering** — "tranquil spot near jungle, off main road" → AI matches
- 📱 **Telegram bot** — instant notifications + interactive search on mobile
- 💬 **MCP server** — query everything conversationally from Claude Desktop
- 🐳 **Production-ready** — Docker, CI/CD, structured logs, health checks, integration tests
- 🆓 **Free hosting** — runs on Oracle Cloud Always Free (4 ARM cores, 24 GB RAM)

## 🛠️ Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 9 |
| MCP | `ModelContextProtocol` (official Anthropic + Microsoft SDK) |
| Database | SQLite + EF Core 9 (snake_case via `EFCore.NamingConventions`) |
| Scraping | Playwright for .NET (headless Chromium) |
| AI | Anthropic Claude API (`Anthropic.SDK`) |
| Bot | Telegram.Bot library |
| Background | `BackgroundService` + polling loops |
| Observability | Serilog + Seq (centralized via Docker network) |
| Tests | xUnit with SQLite tempfiles; coverage via coverlet → Codecov |
| Versioning | MinVer (git-tag-driven semver) |
| CI/CD | GitHub Actions → multi-arch Docker → ghcr.io → Oracle Cloud |

## 🚀 Quick Start

```bash
git clone https://github.com/Janry/rental-radar.git
cd rental-radar

# .NET tools (dotnet-ef, husky, MinVer hook)
dotnet tool restore
dotnet husky install              # активує pre-commit hook (один раз на клон)

# Локально: збірка
dotnet build RentalRadar.slnx
dotnet test  tests/RR.IntegrationTests/RR.IntegrationTests.csproj

# Production: Docker стек (scraper + telegram-bot + seq)
cp .env.example .env              # додати ANTHROPIC_API_KEY, TELEGRAM_BOT_TOKEN
docker compose -f docker/docker-compose.yml up -d
```

Деталі по кожній частині:
- [docs/MCP_SETUP.md](docs/MCP_SETUP.md) — підключити Claude Desktop
- [docs/SCRAPER_SETUP.md](docs/SCRAPER_SETUP.md) — FB session + перший scrape
- [docs/TELEGRAM_SETUP.md](docs/TELEGRAM_SETUP.md) — створення бота + онбординг
- [docs/AI_EXTRACTION.md](docs/AI_EXTRACTION.md) — Claude prompt + cost
- [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) — Oracle Cloud Always Free deploy

## 📋 MCP Tools

| Tool | Purpose |
|------|---------|
| `add_location` | Add a new monitored city/island |
| `list_locations` | Show all monitored locations |
| `discover_sources` | (stub — Phase 3b deferred; manual `add_source` instead) |
| `add_source` | Add FB-group URL(s) for a location |
| `list_sources` / `set_source_enabled` / `remove_source` | Manage sources |
| `search_rentals` | Query listings with structured filters |
| `get_listing_details` | Fetch full info for one listing |
| `create_notification_filter` | Set up real-time Telegram alerts |
| `list_my_filters` / `delete_filter` | Manage subscriptions |

## 💬 Example conversations

> *— I'm moving to Chiang Mai for 3 months. Set up search for condos under 15k baht.*
>
> *— [Claude → add_location → discover_sources → create_filter] Added Chiang Mai. Found 6 active rental groups. Filter "Condos CM under 15k" is live. First matches in Telegram in ~5 min.*

> *— Anything new in Sri Thanu yesterday?*
>
> *— [Claude → search_rentals] 3 new listings: bungalow 12k with hot water, studio 9k, house 18k. Want details?*

## ⚖️ Legal & Ethics

This project scrapes **publicly visible** content from Facebook groups for **personal use**. It respects rate limits, doesn't bypass authentication walls, and stores no PII beyond public author names already visible in posts. See [docs/LEGAL.md](docs/LEGAL.md).

## 📐 Project Structure

```
src/
  RR.Core/              Domain (Location, ScrapeSource, Listing, UserFilter) + abstractions
  RR.Infrastructure/    EF Core (SQLite), repositories, Playwright scraper, Claude extractor, matching engine
  RR.McpServer/         MCP server + tool definitions (stdio для Claude Desktop)
  RR.Scraper.Worker/    BackgroundService — pass-based FB scrape → AI extract → SQLite
  RR.TelegramBot/       NotificationDispatchService + BotPollingService
tools/
  FbLogin/              Console утиліта для одноразового логіну в FB (поза .slnx)
tests/
  RR.IntegrationTests/  xUnit з SQLite tempfile; покриває repos, scraping pass, AI extractor, matching
docker/                 Compose + Dockerfile.scraper + Dockerfile.telegrambot + backup script
docs/                   MCP / scraper / telegram / AI / deployment guides
.github/workflows/      CI: build + test + coverage + multi-arch image push to ghcr.io
.github/dependabot.yml  Automated dependency updates (NuGet + Docker + Actions)
.husky/                 Pre-commit hook (dotnet build)
```

## 🎯 Roadmap

- [x] **Phase 1**: Core domain (Location-agnostic) + MCP server skeleton
- [x] **Phase 2**: EF Core + SQLite + repositories (pivoted from Postgres after cost/complexity review)
- [x] **Phase 3a**: Manual `add_source` via MCP (Phase 3b auto-discovery deferred — not needed for MVP)
- [x] **Phase 4**: Playwright Facebook scraper + Worker
- [x] **Phase 5**: Claude AI extraction pipeline with tool_use + prompt caching
- [x] **Phase 6**: Telegram bot + matching engine (structural + semantic)
- [x] **Phase 7**: Docker stack + Oracle Cloud Always Free deployment
- [x] **Phase 8**: CI polish — coverage, Dependabot, MinVer, Husky.Net, PR template
- [ ] **Phase 9**: Web dashboard (optional)
- [ ] **Phase 3b**: Auto-discovery (revisit if manual flow ever becomes a bottleneck)

## 📄 License

MIT — fork it and adapt for your city.
