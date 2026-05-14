# HANDOFF — Project complete

> Усі заплановані фази (1, 2, 3a, 4-9) закриті.
> Phase 3b (auto-discovery) — DEFERRED і ймовірно не потрібний.
> Read `CLAUDE.md` для повної історії рішень.

## Поточний стан проекту

```
9 фаз готові. ~20 .NET тестів проходять. Multi-arch Docker (amd64+arm64).
4 hosted-процеси (McpServer локально + scraper, telegram-bot, dashboard у Docker).
Cost ~$30/month entirely на Anthropic API; решта безкоштовно.
```

### Що тестовано
- Build та unit-тести зелені на CI
- Manual integration через тестові SQLite DB
- Локальний `dotnet run` всіх 4 проектів

### Що не тестовано на проді
- [ ] Реальний deploy на Oracle Cloud Always Free VM
- [ ] FB-селектори на актуальному live FB (можуть потребувати правок у `FacebookScraper.cs`)
- [ ] Codecov upload з реальним report (потрібна public-репо або CODECOV_TOKEN)
- [ ] Docker buildx multi-arch (потрібен push до ghcr.io який тригериться лише на main)

## Що залишилось зробити

### Operations (non-code)
1. Зробити репо public (або налаштувати ghcr.io read через PAT)
2. Створити Oracle Cloud Always Free VM, пройти `docs/DEPLOYMENT.md`
3. Згенерувати fb-session.json через `tools/FbLogin`, scp на VM
4. Створити Telegram-бот через @BotFather, додати token у `.env`
5. Зробити перший `docker compose up -d`, перевірити логи в Seq через SSH-tunnel
6. Перший `/start` у TG-боті → отримати chat_id
7. У Claude Desktop додати локації, sources, фільтри
8. SSH-tunnel на 8080 → відкрити dashboard
9. Першу таг-релізи (`git tag v0.1.0 && git push --tags`) щоб CI зробив versioned Docker images

### Code follow-ups, якщо колись захочеться
- **Phase 9b: real-time dashboard** — SignalR push нових listings (зараз через manual refresh)
- **Phase 9c: cost dashboard** — `UsageLog` entity + capture з `MessageResponse.Usage`
- **Phase 3b: auto-discovery** — Playwright FB-search + Claude ranker, коли manual flow стане bottleneck
- **Interactive TG commands** — `/pause`, `/resume`, `/filters` (зараз тільки `/start`)
- **Listing-source quality score** — agregate з `ConsecutiveFailures`, `MemberCount`, кількості matched listings
- **Multi-user dashboard** — поки single-user через простий password; для multi-user — додати users table, role-based access

## Архітектурні рішення (рекап)

| Рішення | Замість | Чому |
|---|---|---|
| **SQLite** | PostgreSQL | TTL listings + tiny config — не варто PG-інфри. Refactor PG: swap провайдер + перегенерити migrations |
| **Manual `add_source`** | Auto-discovery | Manual flow покриває все що треба для Ko Phangan; auto-discovery — крихкий проти FB DOM |
| **Session JSON** | Throwaway email/password | FB агресивно банить auto-login патерн; pre-authenticated session живе місяцями |
| **3 Docker hosts** (scraper / bot / dashboard) | Один monolith | Ізоляція збоїв; залишок (McpServer) на ноуті через stdio |
| **Cookie auth з env password** | Identity з users table | Single-user, SSH-tunnel; PBKDF2 буде потрібен лише при public exposure |
| **MudBlazor** | Plain Bootstrap | Portfolio-quality UI з нульовим CSS investment |

## Якщо щось зламається

1. **FB DOM поламався** — селектори у [FacebookScraper.cs](src/RR.Infrastructure/Scraping/FacebookScraper.cs) як константи нагорі. Відкрити DevTools на проблемній групі, оновити `ArticleSelector` чи `LoginWallIndicator`, redeploy.

2. **FB session протух** — `dotnet run --project tools/FbLogin -- new-session.json` локально → scp на VM → `docker compose restart scraper`.

3. **Claude API rate-limit** — в логах буде HTTP 429. Збільшити затримки між sources у `ScrapingOptions.MaxDelayBetweenSourcesSec`.

4. **Dashboard 500 / DB locked** — SQLite WAL mode уже встановлений; рідко падає. Якщо так — `docker compose restart dashboard`.

5. **Telegram bot не шле** — перевірити `TELEGRAM_BOT_TOKEN`, що TG-юзер вже надсилав `/start` (Telegram не дозволяє писати першим без user-initiated chat).

## Final architecture diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                  Your laptop (anywhere)                          │
│                                                                  │
│  Claude Desktop ──stdio──▶ RR.McpServer ──┐                     │
│                                            │                     │
│  Web browser (SSH-tunnel:8080) ───────────│─┐                  │
└────────────────────────────────────────────│─│──────────────────┘
                                              │ │
                                              ▼ ▼
┌─────────────────────────────────────────────────────────────────┐
│              Oracle Cloud Always Free VM (Docker)                │
│                                                                  │
│  ┌─────────────────┐    ┌──────────────┐    ┌────────────────┐ │
│  │ RR.Scraper      │──▶│  SQLite       │◀──│ RR.Dashboard    │ │
│  │ Worker          │    │  (WAL mode)   │    │ (Blazor)        │ │
│  │ Playwright+AI   │    │  one file     │    │ MudBlazor UI    │ │
│  └─────────────────┘    └──────┬───────┘    └────────────────┘ │
│                                 │                                │
│                          ┌──────┴──────────┐                    │
│                          │ RR.TelegramBot  │──▶ user's TG       │
│                          │ matching+notify │                    │
│                          └─────────────────┘                    │
│                                                                  │
│  All workers ──logs──▶ Seq (port 5341, SSH-tunnel only)         │
└──────────────────────────────────────────────────────────────────┘
```

Готово. Час пускати у live.
