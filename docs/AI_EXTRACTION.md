# AI extraction (Phase 5)

`ClaudeAiListingExtractor` отримує `RawListing` від scraper-а, дзвонить Claude API з force-tool_use, і повертає структурований `Listing` (або `null` якщо це не оголошення про оренду).

## Як це підключено

```
RawListing (FB пост) → ClaudeAiListingExtractor → Listing (з price/area/...)
                              │
                              ▼
                       Anthropic.SDK
                              │
                              ▼
                       Claude Haiku 4.5
```

Реєстрація в DI (`RR.Infrastructure.DependencyInjection`):
```csharp
services.Configure<AnthropicOptions>(cfg.GetSection("Anthropic"));
services.AddSingleton<IClaudeClient, AnthropicSdkClient>();
services.AddSingleton<IAiListingExtractor, ClaudeAiListingExtractor>();
```

## Config

`appsettings.json` (Worker):
```json
"Anthropic": {
  "Model": "claude-haiku-4-5-20251001",
  "MaxTokens": 1024
}
```

`ANTHROPIC_API_KEY` — змінна оточення. Anthropic.SDK читає її автоматично (через `new AnthropicClient()` без аргументів). Хочете перевизначити модель — `Anthropic__Model=claude-sonnet-4-6` через env.

## Tool schema

Claude викликає функцію `extract_rental_listing` зі схемою:

| Поле | Тип | Опис |
|---|---|---|
| `is_rental_post` | bool (required) | true якщо це довгострокова оренда |
| `confidence` | number 0-1 (required) | впевненість у класифікації + extraction |
| `price_per_month` | number? | у валюті локації, конвертується якщо інша |
| `area` | string? | район, з `Location.Areas` де можливо |
| `bedrooms` | int? | |
| `property_type` | enum? | Bungalow/House/Apartment/Studio/Villa/Room |
| `pets_allowed` | bool? | |
| `has_pool` / `has_hot_water` / `has_wifi` | bool? | |
| `available_from` | ISO date? | YYYY-MM-DD |
| `contact_info` | string[] | phone/LINE/WhatsApp/Telegram з тексту |

Force-tool_use (`ToolChoice.Type = Tool`) гарантує що Claude **не** відповість текстом — лише структурованим викликом.

## Prompt caching

System prompt (~600 токенів з правилами обробки) ідентичний між викликами. Через `PromptCacheType.AutomaticToolsAndSystem` SDK ставить `cache_control` на нього + на tools. Перший виклик "розігріває" кеш, наступні в межах 5 хв читають з кешу = -90% input cost.

Дивитись у логах Claude API (через `MessageResponse.Usage.CacheCreationInputTokens` / `CacheReadInputTokens`) щоб переконатись що кеш працює.

## Cost estimate

- 1 post → ~1000 input tokens (system + user) + ~150 output → Haiku ≈ $0.0012 / call (на свіжому кеші)
- З кешем — ~$0.0006/call
- Реалістичний обсяг: 10 локацій × 5 sources × 30 нових постів/добу = 1500 calls/добу
- → **~$1/добу = ~$30/міс**

Якщо забагато: можна додати pre-filter (regex на ключові слова "rent", "for rent", "оренда", "เช่า") перед AI-викликом — половина FB-груп засмічена не-rental постами які можна викинути без Claude.

## Тести

`tests/RR.IntegrationTests/ClaudeAiListingExtractorTests.cs` — 4 кейси з фейковим `IClaudeClient`:
1. Rental post → всі поля заповнені у Listing
2. Non-rental → null
3. HTTP exception → null + log warning (scraper не падає)
4. Tool_use відсутній → null

Інтерфейс `IClaudeClient` — тонка обгортка над SDK заради testability. Тести не дзвонять реальний API.

## Майбутнє

Phase 5b (опційно):
- Pre-filter regex для скорочення викликів
- Batching API (50% off) для архівного reindex'у
- Fallback на Sonnet при `confidence < 0.5` — більш точна модель додатково перевірить
