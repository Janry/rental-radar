# HANDOFF — Phase 5: AI extraction (Claude API)

> Phase 4 (FB scraper + Worker) is **DONE** (потребує тільки реального FB session для першого прогону).
> Phase 3b (auto-discovery) залишається **DEFERRED**.
> Read `CLAUDE.md` first.

## Where things stand

- Scraper Worker крутиться, заходить у FB-групи через session JSON, добуває `RawListing` і пише `Listing` в БД.
- Структуровані поля (PricePerMonth, Area, Bedrooms, PetsAllowed, тощо) залишаються `null` бо `StubAiListingExtractor` тільки копіює сирий текст.
- `IAiListingExtractor` — інтерфейс готовий, чекає реалізації проти Claude API.
- В `RR.Infrastructure.csproj` уже є `Anthropic.SDK` v5.0.

## 🎯 Goal of Phase 5

Замінити `StubAiListingExtractor` на `ClaudeAiListingExtractor` який:
- Бере `RawListing.Text` + контекст `Location` (currency, areas)
- Дзвонить Claude API (Haiku — дешево і швидко для цього кейсу)
- Парсить відповідь у структуровані поля `Listing`
- Виставляє `ConfidenceScore` 0.0-1.0
- Повертає `null` якщо AI визначає що це **не** rental post (спам, оголошення про продаж, питання)

## Concrete deliverables

1. **`ClaudeAiListingExtractor` в `RR.Infrastructure/Ai/`**:
   - Inject `IAnthropicClient` (з Anthropic.SDK), `ILogger`
   - System prompt: чітко описує що ми витягуємо + JSON-схему очікуваного output
   - User prompt: `RawText` + контекст `"This is a post in {location.Name}, {location.Country}. Currency: {location.Currency}. Known areas: {areas.join(', ')}."`
   - Використати `tool_use` API щоб Claude повернув строго typed JSON (надійніше за вільну відповідь)
   - На помилку API: лог + повернути `null` (не падати — scraper продовжить наступний post)

2. **JSON-схема для tool_use** (інлайн у extractor):
   ```jsonc
   {
     "type": "object",
     "properties": {
       "is_rental_post": { "type": "boolean" },
       "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
       "price_per_month": { "type": ["number", "null"] },
       "area": { "type": ["string", "null"] },
       "bedrooms": { "type": ["integer", "null"] },
       "property_type": { "enum": ["Bungalow", "House", "Apartment", "Studio", "Villa", "Room", null] },
       "pets_allowed": { "type": ["boolean", "null"] },
       "has_pool": { "type": ["boolean", "null"] },
       "has_hot_water": { "type": ["boolean", "null"] },
       "has_wifi": { "type": ["boolean", "null"] },
       "available_from": { "type": ["string", "null"], "format": "date" },
       "contact_info": { "type": "array", "items": "string" }
     },
     "required": ["is_rental_post", "confidence"]
   }
   ```

3. **`appsettings.json` додатки**:
   - `Anthropic.ApiKey` (читається з env `ANTHROPIC_API_KEY` через `EnvironmentVariables` provider)
   - `Anthropic.Model` (default `claude-haiku-4-5-20251001`)
   - `Anthropic.MaxConcurrent` (default 3) — щоб не вбити rate limit Anthropic API при першому проході великої групи

4. **DI**: `services.AddSingleton<IAiListingExtractor, ClaudeAiListingExtractor>()` (заміна StubAiListingExtractor). HTTP client lifecycle — Anthropic.SDK сам menedge.

5. **Тести**:
   - Unit-тест на сам extractor з мокнутим `IAnthropicClient` (повертає canned JSON через tool_use)
   - Кейси: rental → всі поля заповнені; не-rental → null; помилка API → null + log

6. **Prompt caching** — Anthropic API підтримує prompt caching (`cache_control` на system prompt). System prompt довгий і ідентичний між викликами → кешуємо. Економить токени і час.

## Open questions (тактичні)

- **Який модель?** Haiku 4.5 — швидко (~1s per call), дешево ($1/M input). Sonnet 4.6 точніший але в 5-10x дорожче. Default Haiku, fallback на Sonnet за конфігом.
- **Що з не-англомовним постом?** Багато оголошень в Ko Phangan російською/тайською. Claude розуміє багатомовне; system prompt має це згадати.
- **Що з постами де ціна "DM me" / "по запиту"?** `price_per_month = null`, `confidence` лишається високою. Listing не зникає — користувач може фільтрувати "тільки з ціною".
- **Batching** — Claude API дозволяє batch (50% знижка). Для періодичного scraping не критично, але якщо колись зробимо повний reindex архіву, batch буде потрібен.

## Cost estimate

- 1 post → ~1000 input tokens (system + user) + ~150 output → Haiku ≈ $0.0012 / call
- Реалістичний обсяг: 10 локацій × 5 sources × 30 нових постів/добу = 1500 calls/добу = ~$1.80/добу
- Кешування system prompt зріже ~40%, реально буде ~$1/добу = ~$30/міс
- Якщо це багато — можна не запускати extraction на кожному post-у, а лише на тих що матчать manual фільтри (price/area regex)

## Definition of Done for Phase 5

- [ ] `ClaudeAiListingExtractor` працює end-to-end на реальних 10+ FB-постах з Ko Phangan
- [ ] `is_rental_post=false` посты правильно фільтруються (`null` повертається)
- [ ] `search_rentals` MCP-tool тепер повертає не лише raw_text а й structured price/area/тощо
- [ ] Unit-тест з мокнутим клієнтом проходить (3+ кейси)
- [ ] System prompt використовує prompt caching
- [ ] `docs/AI_EXTRACTION.md` з прикладом prompt + sample response + cost notes

## Phase 3b (auto-discovery) — when to come back

After Phase 5+6 produce notifications and the system delivers value. By then we'll know whether discovery is even needed (probably not — manual covers Ko Phangan completely).
