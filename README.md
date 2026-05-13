# 📡 RentalRadar

> **AI-powered rental listings radar — works for any city, island, or neighborhood.**
> Scrapes Facebook groups & marketplaces, structures messy posts with Claude AI,
> and pings you on Telegram the moment a matching listing appears.

[![.NET 9](https://img.shields.io/badge/.NET-9.0-purple)](https://dotnet.microsoft.com)
[![MCP](https://img.shields.io/badge/MCP-0.3-blue)](https://modelcontextprotocol.io)
[![Docker](https://img.shields.io/badge/Docker-ready-blue)](https://docker.com)
[![CI](https://github.com/USERNAME/rental-radar/actions/workflows/ci.yml/badge.svg)](https://github.com/USERNAME/rental-radar/actions)

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
| Database | PostgreSQL 16 + EF Core 9 |
| Scraping | Playwright for .NET (headless Chromium) |
| AI | Anthropic Claude API (`Anthropic.SDK`) |
| Bot | Telegram.Bot library |
| Background | `BackgroundService` + cron schedules |
| Observability | Serilog + OpenTelemetry + Seq |
| Tests | xUnit + Testcontainers (real PG in CI) |
| CI/CD | GitHub Actions → Oracle Cloud |

## 🚀 Quick Start

```bash
git clone https://github.com/USERNAME/rental-radar.git
cd rental-radar
cp .env.example .env  # add Anthropic + Telegram keys
docker compose up -d
```

Then connect Claude Desktop — see [docs/MCP_SETUP.md](docs/MCP_SETUP.md).

## 📋 MCP Tools

| Tool | Purpose |
|------|---------|
| `add_location` | Add a new monitored city/island + auto-find sources |
| `list_locations` | Show all monitored locations |
| `discover_sources` | Re-run source auto-discovery for a location |
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
  RR.Core/              Domain (Location, ScrapeSource, Listing, UserFilter) + interfaces
  RR.Infrastructure/    EF Core, repositories, FB Playwright adapter, Claude client
  RR.McpServer/         MCP server + tool definitions (Claude Desktop integration)
  RR.Scraper.Worker/    BackgroundService that scrapes on schedule
  RR.TelegramBot/       Notification dispatcher + interactive bot
  RR.AiFilter/          Claude-powered extraction & semantic matching
tests/
  RR.UnitTests/         Pure unit tests (no I/O)
  RR.IntegrationTests/  Testcontainers-based DB + API tests
docker/                 Compose + Dockerfiles
.github/workflows/      CI on PRs, CD on main
```

## 🎯 Roadmap

- [x] **Phase 1**: Core domain (Location-agnostic) + MCP server skeleton
- [ ] **Phase 2**: EF Core + PostgreSQL + repositories
- [ ] **Phase 3**: Source auto-discovery (FB group search by keywords)
- [ ] **Phase 4**: Playwright Facebook scraper
- [ ] **Phase 5**: Claude AI extraction pipeline
- [ ] **Phase 6**: Telegram bot + notification engine
- [ ] **Phase 7**: Docker + Oracle Cloud deployment
- [ ] **Phase 8**: Integration tests + CI/CD
- [ ] **Phase 9**: Web dashboard (optional)

## 📄 License

MIT — fork it and adapt for your city.
