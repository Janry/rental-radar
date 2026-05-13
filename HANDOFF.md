# HANDOFF — Phase 6: Telegram bot + matching engine

> Phase 5 (Claude API extraction) is **DONE**.
> Phase 3b (auto-discovery) залишається **DEFERRED**.
> Read `CLAUDE.md` first.

## Where things stand

- Scraper Worker працює end-to-end: бере enabled sources → парсить пости через Playwright → Claude витягує структуровані поля → `Listing` рядки у БД з price/area/тощо.
- `UserFilter` створюються через MCP (`create_notification_filter`) — лежать у БД, але **ніхто їх не матчить** з listings і **нікуди не пушить**.
- `IMatchingEngine` + `INotificationDispatcher` — інтерфейси готові в Core, обидва без реалізації.

## 🎯 Goal of Phase 6

З'єднати останній шматок: коли scraper зберігає новий `Listing` — знайти всі активні `UserFilter`, що матчать (за price/area/property/semantic), і відправити нотифу в Telegram.

Після цього: я (користувач) реально отримую сповіщення про нові оренди в Ko Phangan на телефон.

## Concrete deliverables

1. **`MatchingEngine : IMatchingEngine`** в `RR.Infrastructure/Matching/`:
   - Структурний матчінг (price range, area exact match, property_type IN list, bedrooms ≥) — простий LINQ
   - Semantic matching (якщо `filter.SemanticQuery` not null): окремий Claude-виклик з system prompt типу "Does this listing match the user's natural-language query? Return match/no-match + reason".
     - Опційно: pre-filter тільки структурно проходить → AI бачить лише candidates → економить токени
   - Повертає `IReadOnlyList<UserFilter>` що матчать
   - Тест: 5+ кейсів (price нижче, ціна вище, area match/no-match, semantic match/no-match)

2. **`TelegramNotificationDispatcher : INotificationDispatcher`** в `RR.Infrastructure/Telegram/`:
   - `Telegram.Bot` NuGet (додати у `RR.Infrastructure.csproj`, версія ~19+)
   - Формат повідомлення: коротке summary (price, area, bedrooms) + `tap to view` deeplink на FB post + перше фото
   - Markdown-формат, escape spec-символів
   - Rate limit: Telegram дозволяє 30 msg/sec на bot, нам цього з запасом
   - `TELEGRAM_BOT_TOKEN` з env

3. **`RR.TelegramBot` проект** — його csproj не існує (декларувався в Phase 1):
   - .NET 9 Worker Service (як RR.Scraper.Worker)
   - Дві паралельні задачі:
     - `NotificationDispatchService : BackgroundService` — polls `GetUnprocessedListingsAsync` → matching → dispatch → mark processed
     - `TelegramBotPollingService : BackgroundService` — слухає бота, обробляє команди `/start`, `/filters`, `/pause` тощо (опційно у Phase 6, або deferred)

4. **`Listing.ProcessedAt`** — використати як прапор "вже зматчили + повідомили". Polling SELECT WHERE processed_at IS NULL.

5. **Інтеграційний тест на матчінг + dispatcher**:
   - Fake `ITelegramBotClient` (Telegram.Bot вже має `IBotClient` interface)
   - Заповнити БД 3 listings + 2 filters → запустити одну ітерацію → assert які `SendMessageAsync` виклики зробились

6. **`docs/TELEGRAM_SETUP.md`** — як створити бота через @BotFather, дістати token, додати у `.env`.

## Open questions

- **Куди ставити NotificationDispatchService — у RR.TelegramBot чи у RR.Scraper.Worker?** Логічніше в TelegramBot (це його responsibility), але scraper уже має access до `Listing` через DbContext і ProcessedAt оновлення може ділитися транзакцією. Trade-off: окремий процес = ізоляція збоїв; один процес = простіше.
  Default: окремий, у TelegramBot.
- **Як часто polling?** scraper працює раз/15 хв. Notification dispatcher може polling-ом раз/1 хв або через signal (Telegram Worker слухає file-system watcher на .db). Простіший і resilient — polling раз/1 хв.
- **Semantic matching вартість**: якщо у юзера є `SemanticQuery`, кожний listing що пройшов структурний фільтр коштує ~$0.001/виклик. Реалістично — 10-30 listings/день що проходять структурний → ~$0.30/міс на користувача. Прийнятно.

## Definition of Done for Phase 6

- [ ] Реальне TG-повідомлення приходить на мій chat-id коли scraper додає матчевий listing
- [ ] `Listing.ProcessedAt` виставляється після dispatch (idempotency: повторний пас не повторює)
- [ ] Структурний фільтр працює коректно (price range, area, property_type, bedrooms)
- [ ] Semantic фільтр викликає Claude і відсіває не-матчі
- [ ] Інтеграційний тест з fake bot client проходить
- [ ] `RR.TelegramBot` додано у `.slnx`
- [ ] `docs/TELEGRAM_SETUP.md`
