# HANDOFF — Phase 9 (optional): Web dashboard

> Phase 8 (CI polish) is **DONE**. Всі основні фази закриті.
> Phase 3b (auto-discovery) — **DEFERRED** і ймовірно вже не знадобиться.
> Read `CLAUDE.md` first.

## Where things stand

Pipeline працює end-to-end, інфра production-ready:
- McpServer (локально з Claude Desktop) — додати локацію, керувати фільтрами, шукати оголошення
- Scraper Worker (Docker) — періодично скрапить FB через Playwright + Claude витяг полів
- TelegramBot (Docker) — матчить + шле в TG, `/start` повертає chat_id
- Seq — централізовані логи
- CI: build + test + coverage → Codecov, multi-arch image push → ghcr.io
- Dependabot, MinVer, Husky.Net, PR template — operational hygiene
- Backup script для SQLite, deployment guide для Oracle Cloud Always Free

Що залишилось зробити на live-side (без коду):
- Перший реальний deploy на Oracle VM
- Перевірити що FB-селектори актуальні (можливо доведеться поправити константи у `FacebookScraper.cs`)
- Підняти Codecov акаунт + (опційно) додати CODECOV_TOKEN secret якщо репо приватний

## 🎯 Phase 9 (optional) — Web dashboard

Якщо колись захочеться UI окрім Claude Desktop і Telegram:

### Concept
Single-page Blazor Server app — авторизація cookie-based з одним юзером (паролем з env), деплоїться у тому ж docker-compose. Показує:
- **Live listings stream** — нові оголошення з нагрівом по match-score
- **Filter CRUD** — створити/редагувати/паузити фільтри без MCP
- **Source health** — таблиця sources з `LastScrapedAt`, `ConsecutiveFailures`, кнопка `enable/disable`
- **Cost dashboard** — графік Anthropic API spend з token usage (потребує `Usage` логування у Claude extractor)
- **Manual scrape trigger** — кнопка "scrape now" для конкретного source без чекання таймеру

### Why Blazor Server (а не SPA + REST API)
- Один процес, одна авторизація, SignalR-канал для real-time оновлень listings
- Не треба окремий API layer — share types з Core
- Той самий .NET 9, той самий DI
- Trade-off: stateful connections (треба sticky session якщо multi-instance), але single-user це не проблема

### Architecture sketch
```
src/
  RR.Dashboard/                 нова Blazor Server проект
    Components/
      Layout/                   AppShell з sidebar
      Pages/
        Listings.razor          live stream через SignalR
        Filters.razor           CRUD таблиця
        Sources.razor           управління джерелами
        Costs.razor             API spend graph
      Shared/                   reusable
    Services/
      ListingFeedService.cs     SignalR-stream нових listings
      DashboardAuthOptions.cs   single password з env
    Program.cs
    appsettings.json
docker/
  Dockerfile.dashboard          aspnet:9.0 base
```

### Deliverables

1. `RR.Dashboard` проект (.NET 9 Blazor Server, в `.slnx`)
2. Cookie auth з single user — пароль з env `DASHBOARD_PASSWORD` (hash з PBKDF2 / scrypt у production)
3. 4 сторінки (Listings / Filters / Sources / Costs)
4. SignalR hub для live listings push — додати `INotificationDispatcher.NotifyAsync` hook чи окремий broadcast service
5. Cost tracking — додати `UsageLog` entity у БД, заповнює `ClaudeAiListingExtractor` через `response.Usage`
6. Docker container: `mcr.microsoft.com/dotnet/aspnet:9.0` base, новий сервіс у docker-compose, експонувати тільки за reverse-proxy / SSH-tunnel
7. Integration tests на Blazor components (`bUnit`) — мінімум для Listings + Filters
8. `docs/DASHBOARD_SETUP.md`

### Open questions
- **Public exposure?** Якщо так — потрібен Caddy/Traefik reverse-proxy з Let's Encrypt. Я б тримав за SSH-tunnel або Tailscale.
- **Real-time оновлення** — SignalR з push при ProcessedAt update, чи simple polling раз/30с? Push драматичніший для дема, polling простіший.
- **Mobile-ready?** Blazor Server рендериться на сервері, mobile UX OK з MudBlazor чи similar. Native PWA — overkill.

## Cost estimate for Phase 9
- ~4-8 годин роботи
- Інфра: +1 контейнер у Docker (~50 MB RAM idle), $0 incremental на VM
- Без додаткового API spend

## Якщо НЕ робити Phase 9

Проект і так closed:
- Claude Desktop через MCP — повноцінне керування
- TG нотифи — основний UX
- Logs у Seq + git-репо як audit trail

Phase 9 — це чистий portfolio polish ("full-stack demonstration"), не функціональна необхідність.

---

## Final state of the project

```
8 phases done. ~20 .NET tests. Multi-arch Docker. Multi-process production stack.
Cost ~$30/month entirely on Anthropic API; everything else free.
```

Якщо все працює — час підняти Oracle VM і перевірити end-to-end. Якщо хочеться додати щось — Phase 9 чекає окремої сесії.
