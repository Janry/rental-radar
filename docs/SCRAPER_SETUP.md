# Запуск Scraper Worker (Phase 4)

Worker — окремий процес, який періодично заходить у всі enabled FB-джерела і складає нові оголошення в БД. AI-екстракція даних — Phase 5; зараз stub.

## Одноразова підготовка

### 1. Встановити Playwright browsers

Playwright требує Chromium. Встановлюємо як local-tool у репо:

```bash
dotnet tool install --local Microsoft.Playwright.CLI
dotnet playwright install chromium
```

Це підвантажить ~150 MB Chromium у user-cache (`%LOCALAPPDATA%/ms-playwright` на Windows, `~/.cache/ms-playwright` на Linux).

### 2. Створити окремий FB-акаунт (рекомендовано)

Для скрапінгу **НЕ** використовуйте основний акаунт — є ризик бану. Створіть "tier-2" акаунт:
- Реалістичне ім'я + фото
- Тиждень-два "розігрівайте": зайдіть кілька разів, додайте 2-3 друзів, відреагуйте на пости в одній-двох групах
- Тільки після цього використовуйте для скрапу

### 3. Згенерувати session JSON

```bash
mkdir -p src/RR.Scraper.Worker/data
dotnet run --project tools/FbLogin -- src/RR.Scraper.Worker/data/fb-session.json
```

Що буде:
1. Відкриється Chromium (НЕ headless)
2. Залогіньтесь у FB вручну, пройдіть 2FA якщо є
3. Дочекайтесь що з'явилася стрічка
4. Поверніться в термінал, натисніть ENTER
5. JSON з cookies збережеться за вказаним шляхом

> Сесія живе місяцями. Коли запротухне (Worker почне отримувати `login wall` помилку) — повторіть цей крок.

### 4. Прокинути шлях у конфіг

`appsettings.json` Worker-а вже має дефолтний шлях `data/fb-session.json`. Якщо інший — задайте env-vars:

```bash
$env:FACEBOOK_SESSION_PATH = "D:/абсолютний/шлях/fb-session.json"
$env:ConnectionStrings__Default = "Data Source=D:/абс/шлях/rental_radar.db;Cache=Shared;Foreign Keys=True"
```

Worker і MCP server мають **дивитись на одну БД** (інакше MCP додає Locations які Worker не побачить).

## Запуск

```bash
dotnet run --project src/RR.Scraper.Worker
```

Очікувано в логах при першому проході:
```
PRAGMA journal_mode=WAL applied
Pass start — N enabled sources
Chromium launched
Scraping <name> at <url>
Found <X> article elements on <url>
<name>: +Y new / Z skipped
Pass complete — N sources, +M new listings
```

Якщо session не валідна — Worker не падає, лиш виводить warning. Запустіть FbLogin знов.

## Перевірка результату

```bash
sqlite3 src/RR.Scraper.Worker/data/rental_radar.db "SELECT count(*) FROM listings;"
```

Або через MCP в Claude Desktop: *"Покажи мені оренди в Ko Phangan за останній тиждень"* — викличе `search_rentals`.

## Типові проблеми

| Симптом | Причина | Що робити |
|---|---|---|
| `FB redirected to login wall` | сесія протухла | повторити FbLogin |
| `No articles found within 15000ms` | селектор `[role="article"]` змінився, або сторінка не довантажилась | подивитись HTML вручну, оновити `ArticleSelector` у `FacebookScraper.cs` |
| `<source> hit 5 consecutive failures — auto-disabled` | FB чи специфічна група blocking | передивитись логи, можливо потрібен новий акаунт; знов увімкнути через MCP `set_source_enabled` |
| Worker не бачить локацій додані через MCP | різні connection strings | переконатись що `ConnectionStrings__Default` ідентичний для McpServer і Worker |

## Що **НЕ** робить Phase 4 (за дизайном)

- Не парсить структуровані поля (price, area, bedrooms тощо) — це Phase 5 з Claude API.
- Не шле нотифи в Telegram — Phase 6.
- Не запускається в Docker — Phase 7.
