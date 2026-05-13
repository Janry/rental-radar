# HANDOFF — Phase 7: Docker + Oracle Cloud deployment

> Phase 6 (Telegram bot + matching engine) is **DONE**. Pipeline працює end-to-end локально.
> Phase 3b (auto-discovery) залишається **DEFERRED** — не потрібний для MVP.
> Read `CLAUDE.md` first.

## Where things stand

- Три hosted-процеси готові: `RR.McpServer`, `RR.Scraper.Worker`, `RR.TelegramBot`
- Plus `tools/FbLogin` для одноразового логіну в FB
- Все ділить одну SQLite БД через WAL-mode
- Локально все запускається через `dotnet run --project ...` після одноразового FbLogin

## 🎯 Goal of Phase 7

Запакувати все у Docker-стек і задеплоїти на Oracle Cloud Always Free VM. Після Phase 7: стек крутиться 24/7 на безкоштовній ARM-машинці, скрапер працює без участі ноута розробника.

## Що треба зробити

1. **Dockerfile для кожного hosted-процесу**:
   - `docker/Dockerfile.scraper` — базовий `mcr.microsoft.com/playwright/dotnet:v1.49.0-jammy` (Chromium вже встановлений)
   - `docker/Dockerfile.telegrambot` — базовий `mcr.microsoft.com/dotnet/aspnet:9.0` (треба ASP.NET Core 9.0 через Telegram.Bot)
   - McpServer не контейнеризуємо — він живе на машині користувача поряд з Claude Desktop

2. **`docker/docker-compose.yml`** оновити:
   - `scraper` сервіс: build з Dockerfile.scraper, env_file `.env`, mount `data/` volume для SQLite + fb-session
   - `telegram-bot` сервіс: build з Dockerfile.telegrambot, env_file `.env`, mount `data/` volume
   - `seq` (уже є) для агрегації логів
   - Healthcheck на Worker-и через файл-touched-recently або останній DB-update

3. **Logging до Seq** (Phase 6 цього ще не зробили):
   - Додати `Serilog` + `Serilog.Sinks.Seq` у Infrastructure
   - Або просто Microsoft.Extensions.Logging + Seq-Sink
   - Worker'и шлють в Seq, McpServer лишається на console (stdio constraint)

4. **`.github/workflows/ci.yml`** доробити (зараз він мабуть базовий):
   - `dotnet build`, `dotnet test`
   - Docker build на push до main
   - Push до GitHub Container Registry (ghcr.io/janry/...)

5. **Oracle Cloud setup інструкція** в `docs/DEPLOYMENT.md`:
   - Як створити Always Free Ampere VM (4 vCPU + 24 GB RAM)
   - Налаштувати SSH, відкрити порти (для Seq UI опційно)
   - `docker compose pull && docker compose up -d`
   - Перший раз: SCP'ом покласти `fb-session.json` + `.env` на VM
   - Як перезайти і оновити session коли FB запротухне

6. **Backup стратегія для SQLite**:
   - Cron на VM: `sqlite3 rental_radar.db ".backup /backups/$(date +%F).db"` раз/добу
   - Опційно: rsync до S3-compatible (Backblaze B2 безкоштовно до 10 GB)

## Open questions

- **Чи деплоїти McpServer у Docker?** Stdio constraint робить це нетривіальним — Claude Desktop запускає його як child process локально. Якщо хочеться віддалено — `mcp-remote` proxy. На Phase 7 я б скіпнув.
- **HTTPS для Seq UI?** Якщо тільки для personal-use і доступ через SSH tunnel — не треба. Якщо публічно — caddy reverse-proxy з Let's Encrypt.
- **ARM build?** Oracle Free VM — ARM64 (Ampere). `mcr.microsoft.com/playwright/dotnet:v1.49.0-jammy` має ARM64 варіант, перевірити. Якщо ні — будемо мучитись з emulation.

## Definition of Done for Phase 7

- [ ] `docker compose up -d` локально піднімає scraper + telegram-bot + seq, працює end-to-end
- [ ] Логи з обох worker'ів видно в Seq UI на http://localhost:5341
- [ ] Github Actions білдить + пушить образи в ghcr.io при push до main
- [ ] На Oracle Cloud VM можна `docker compose pull && docker compose up -d` і воно крутиться
- [ ] Перший FB-scrape з production VM видає реальні listings у тестовому TG-чаті
- [ ] `docs/DEPLOYMENT.md` з кроками

## Phase 3b — все ще deferred

Якщо після місяця-двох production-роботи виявиться що manual flow набридло, або хочеться додавати локації без знання її FB-груп — повертаємось до Phase 3b (Playwright + Claude ranker).
