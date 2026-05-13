# HANDOFF — Phase 4: Facebook scraper

> Phase 3a (manual source management) is **DONE**.
> Phase 3b (auto-discovery via Playwright + AI ranker) is **DEFERRED** until Phase 4+ proves end-to-end value.
> Read `CLAUDE.md` first for project-wide context.

## Decisions baked in before Phase 4 starts

- **FB auth strategy: Session JSON** (NOT throwaway email/password in `.env`).
  Auto-login patterns get FB-banned within days. A pre-authenticated session from a real browser looks legitimate to anti-bot and lasts months. Trade-off: ~30 min upfront tooling + occasional manual re-login when session expires.
- **Account recommendation: dedicated "tier-2" account, не основний користувача.**
  Створити окремий FB-акаунт, теплий (друзі, кілька постів, тиждень-два нормальної активності перед першим автомат-запуском). Якщо забанять — втрата мінімальна. Зберігати окремий пароль у password manager, не в репо.

## Where things stand

- Persistence layer alive (Phase 2): SQLite, EF Core, repos.
- User can manually add FB-group URLs via Claude Desktop using `add_source` MCP tool (Phase 3a).
- `ISourceDiscoveryService` is still a stub — replacing it is what Phase 3b would do, but we deferred until the scraper actually works.
- Domain has `ScrapeSource.IsEnabled`, `LastScrapedAt`, `LastSuccessAt`, `ConsecutiveFailures` — scraper Worker will write to these.

## 🎯 Goal of Phase 4

Implement `IFacebookScraper` so a background Worker can periodically visit each enabled `ScrapeSource`, pull recent posts, hand them to AI extraction (Phase 5), and persist the resulting `Listing` rows.

After this phase: scraper Worker runs every N minutes, new posts arrive in the DB and (eventually) ping the user via Telegram.

## Concrete deliverables

1. **`IFacebookSession` abstraction** in `RR.Core.Abstractions`:
   ```csharp
   public interface IFacebookSession
   {
       Task<IBrowserContext> GetAuthenticatedContextAsync(IBrowser browser, CancellationToken ct = default);
       Task<bool> IsSessionValidAsync(CancellationToken ct = default);
   }
   ```
   Інкапсулює "звідки беруться cookies". Default impl (`FileBasedFacebookSession` in `RR.Infrastructure/Scraping/`) читає JSON з `FACEBOOK_SESSION_PATH`, перевіряє валідність простим heads-up запитом на FB і повертає `BrowserContext` з storageState.

2. **`tools/FbLogin` — окремий console-проект** (новий, у `tools/FbLogin/FbLogin.csproj`):
   - Запускає Playwright headed Chromium
   - Юзер логиниться руками + проходить 2FA якщо є
   - Після успішного логіну (детектимо через наявність елемента типу `nav[role=navigation]`) дампить `context.StorageStateAsync()` у JSON-файл, шлях з args
   - Запуск: `dotnet run --project tools/FbLogin -- path/to/fb-session.json`
   - **НЕ заходить у solution `.slnx`** — це утиліта, не частина основного build (інакше CI потягне Playwright).

3. **`RR.Scraper.Worker` проект** — його csproj не існує (декларувався в Phase 1, файла нема). Створити як .NET 9 Worker Service:
   - `appsettings.json` з `FACEBOOK_SESSION_PATH`, `SCRAPE_INTERVAL_MINUTES`, `LISTING_RETENTION_DAYS`
   - Reference на `RR.Core`, `RR.Infrastructure`
   - DI: `AddInfrastructure` + `AddHostedService<ScrapingBackgroundService>`

4. **`FacebookScraper : IFacebookScraper`** in `RR.Infrastructure/Scraping/`:
   - Бере `IFacebookSession`, отримує authenticated `BrowserContext`
   - Для FB-групи: GET `{source.Url}` → ScrollDown N разів → витягти всі видимі пости (DOM-селектори через `data-pagelet` атрибути; винести у конфіг, бо FB змінює)
   - Кожен пост → `RawListing` з `SourceId`, `ExternalId` (з `data-ft` атрибута поста), `RawText`, `AuthorName`, `ImageUrls`, `PostedAt`
   - `IAsyncEnumerable<RawListing>` — yield'имо по мірі парсингу, щоб не тримати всю групу в пам'яті

5. **`ScrapingBackgroundService : BackgroundService`** в Worker:
   - Loop: `await Task.Delay(SCRAPE_INTERVAL_MINUTES, ct)` → крутимо
   - Один прохід:
     - `cleanup.PruneOldAsync(retention)` — TTL cleanup (Phase 2 hook)
     - `sources.GetEnabledAsync()` → для кожного source:
       - try: `scraper.ScrapeAsync(source)` → для кожного `RawListing`:
         - `if (!listings.ExistsByExternalIdAsync(...))` → передаємо stub-екстрактору, який повертає `Listing` з порожніми структурованими полями (Phase 5 замінить на справжню Claude-екстракцію)
         - `listings.AddAsync(...)`
       - update `LastScrapedAt`, `LastSuccessAt`, скидаємо `ConsecutiveFailures`
     - catch: інкрементуємо `ConsecutiveFailures`, log error, продовжуємо з наступним source
   - Якщо `consecutive_failures > 5` для source — авто `IsEnabled = false`, log warning. Юзер бачитиме в `list_sources`.

6. **Session-expired handling**: якщо `IFacebookSession.IsSessionValidAsync()` повертає false на старті Worker → log error, **НЕ** падати. Worker продовжує крутитись з порожніми циклами, чекаючи поки оператор оновить session JSON. У Phase 6+ можна додати Telegram-нотифу адміну.

7. **Docker** — base image `mcr.microsoft.com/playwright/dotnet:v1.49.0-jammy` (вже має Chromium + всі deps). `docker-compose.yml` mount-ить `FACEBOOK_SESSION_PATH` як read-only volume з хоста.

## What stays out of Phase 4

- **AI extraction** (Phase 5) — використовуємо stub `IAiListingExtractor` який копіює `RawListing.Text` → `Listing.RawText` + нічого більше не виставляє. Філ-and-find пайплайн працює, але `PricePerMonth`, `Area` тощо лишаються null.
- **Auto-discovery** (Phase 3b) — `ISourceDiscoveryService` stub без змін.
- **Telegram нотифи** (Phase 6) — поки нікуди не пушимо.

## Open questions (тактичні, рішення прийму при імплементації)

- **Post-iteration limit** — наскільки далеко скролити в кожній групі? Перший варіант: hardcode 5 scrolls = ~20-30 постів. Конфігурабельне пізніше.
- **Rate-limiting** — randomized `Task.Delay(15-45s)` між sources, ZERO parallel scrapes per session. Один Worker, один browser context, серіально.
- **Browser context lifecycle** — один на весь pass через всі sources, чи fresh на кожен source? Fresh безпечніше (FB менше підозр), але повільніше. Default: один context на pass.
- **External ID stability** — FB не дає стабільного post_id у публічному DOM. Витягуємо з `data-ft` JSON блоба, або fallback на хеш `(author_id + posted_at + first_200_chars)`. Вибрати при імплементації.

## Definition of Done for Phase 4

- [ ] `dotnet run --project tools/FbLogin -- fb-session.json` дає валідний session-JSON
- [ ] `IFacebookSession.IsSessionValidAsync()` повертає true для свіжої сесії, false для подохлої (тест на видалених cookie)
- [ ] Worker крутиться, проходить по реальній FB-групі (Ko Phangan rentals), додає 10+ нових `Listing` рядків у БД на першому проході
- [ ] Повторний прохід **не дублює** — `ExistsByExternalIdAsync` правильно зупиняє додавання
- [ ] `consecutive_failures` росте при штучному ламанні (видалена session) і авто-disable при >5
- [ ] Integration test з замоканим `IFacebookSession` (повертає сторінку з тестовим HTML) — парситься, дедуплікація працює
- [ ] `RR.Scraper.Worker` додано у `.slnx`
- [ ] `tools/FbLogin` **НЕ** у `.slnx`
- [ ] `docs/SCRAPER_SETUP.md` з кроками першого login + куди класти session.json

## Phase 3b (auto-discovery) — when to come back

Pick up Phase 3b once Phase 4-5 produce real listings. By then we'll know:
- Whether manual source management is enough (probably for stable user with <5 locations)
- Whether FB anti-bot heuristics break the scraper anyway (in which case auto-discovery would face same wall)
- What "good" relevance looks like — informing the AI ranker prompt

Sketch is preserved in git history (`HANDOFF.md` before Phase 3a commit) if needed.
