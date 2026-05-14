# Dashboard setup (Phase 9)

`RR.Dashboard` — Blazor Server SPA на MudBlazor. Single-user, cookie-auth з password з env. Деплоїться у тому ж docker-compose поряд з scraper / telegram-bot.

**4 сторінки:**
- **Stats (`/`)** — лічильники: locations, active sources, listings за 7 днів, active filters + recent activity
- **Listings (`/listings`)** — таблиця останніх 100 listings з фільтрами location / price range
- **Filters (`/filters`)** — table з toggle pause/enable + delete; CRUD-create робиться через MCP (там же де керування locations/filters)
- **Sources (`/sources`)** — згруповано по location, toggle enabled + delete, видно last_scraped + consecutive_failures

## Локальний запуск

```bash
$env:ConnectionStrings__Default = "Data Source=D:/абс/шлях/rental_radar.db;Cache=Shared;Foreign Keys=True"
$env:Dashboard__Password = "myStrongPassword"

dotnet run --project src/RR.Dashboard
# відкривається на http://localhost:5xxx
```

## У Docker

```bash
# .env додати:
# Dashboard__Password=changeme
docker compose -f docker/docker-compose.yml up -d dashboard
```

Контейнер слухає port 8080, в compose проксі-маппинг до `127.0.0.1:8080` — **тільки localhost VM-машини**. Назовні не відкриваємо.

## Доступ до production dashboard

Через SSH-tunnel:

```bash
ssh -L 8080:localhost:8080 ubuntu@<vm-ip>
# Залишити термінал відкритим
```

В браузері: http://localhost:8080 → форма логіну → пароль з `.env` → all pages.

## Безпека

**Що зроблено:**
- Cookie-based auth, HttpOnly + SameSite=Lax
- `CryptographicOperations.FixedTimeEquals` для constant-time comparison password
- Antiforgery token на login form
- Bind тільки на `127.0.0.1` у Docker — нема публічної exposure

**Чого НЕ зроблено (acceptable for personal use):**
- Password у `.env` — plaintext. Для public exposure треба зробити PBKDF2-хеш.
- Нема HTTPS — assumes SSH-tunnel (encrypted) або Tailscale
- Нема rate-limit на login — at most один-двох attempts/sec не вб'є SQLite, тож OK

**Якщо хочеться публічно експонувати:**
- Поставити Caddy перед dashboard з Let's Encrypt
- Замінити plaintext password на PBKDF2 hash у env-var
- Додати rate-limit middleware

## Технічні нотатки

- **Database access**: `IDbContextFactory<AppDbContext>` (singleton) — кожна page створює свій short-lived context, бо Blazor Server circuits живуть довго і scoped DbContext тримав би з'єднання надовго.
- **Dashboard не використовує `AddInfrastructure`** — там repositories і scraper-services які dashboard не потребує. Тільки EF Core + auth + MudBlazor.
- **Login flow**: Login.razor — статична Razor компонента (без `@rendermode`), HTML-форма POST на `/auth/login` Minimal API endpoint, який валідує password + виставляє cookie. Це обходить проблему Blazor SSR: інтерактивні компоненти живуть в SignalR-каналі і не можуть встановлювати cookies.

## Що **НЕ** в Phase 9 MVP

- **SignalR live stream** нових listings — поки через refresh button. Real-time push можна додати окремим коммітом (запровадити `IListingNotifier` що broadcast'ить через `HubContext`).
- **Cost dashboard** — потребує `UsageLog` entity + capture з Claude response (`MessageResponse.Usage`). Phase 9b.
- **Manual scrape trigger** — кнопка "scrape now". Потребує scraper-у IPC слухати команди. Не критично, scraper все одно крутиться раз/15 хв.
- **Mobile-first responsive** — MudBlazor responsive by default, але dedicated mobile UX не optimized.
