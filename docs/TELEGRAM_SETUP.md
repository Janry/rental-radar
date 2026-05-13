# Telegram bot setup (Phase 6)

`RR.TelegramBot` — окремий .NET Worker, який:
1. Polling-ом раз/1 хв забирає unprocessed listings, матчить з активними фільтрами, шле нотифи в TG
2. Паралельно слухає incoming messages — поки що тільки `/start` (повертає ваш chat_id для онбордингу)

## Підготовка

### 1. Бот через @BotFather

Якщо ще нема:
1. Відкрити чат з [@BotFather](https://t.me/BotFather)
2. `/newbot` → дати ім'я + username (`*_bot`)
3. Скопіювати token виду `123456:ABC-DEF...`

### 2. Token у конфіг

Передається через env-vars:

```bash
$env:TELEGRAM_BOT_TOKEN = "123456:ABC-DEF..."
$env:ConnectionStrings__Default = "Data Source=D:/абс/шлях/rental_radar.db;Cache=Shared;Foreign Keys=True"
$env:ANTHROPIC_API_KEY = "sk-ant-api03-..."   # для semantic matching через Claude
```

Або в `appsettings.json` Worker-а (не рекомендую — токен у репо).

## Запуск

```bash
dotnet run --project src/RR.TelegramBot
```

Очікувано в логах:
```
PRAGMA journal_mode=WAL applied
Dispatcher start — poll every 60s, batch=50
Bot polling started
```

## Перший фільтр (онбординг)

1. У Telegram знайти свого бота, написати `/start`
2. Бот відповідає: `Ваш chat_id: 123456789`
3. У Claude Desktop запитати:
   > *"Create a notification filter for chat_id 123456789 in Ko Phangan, name 'Студія Шрітану до 12к', max price 12000 THB, areas Sri Thanu, property types Studio."*
4. Claude викличе `create_notification_filter` через MCP. Фільтр потрапить у `user_filters` таблицю.

Далі — як scraper збере нове listing що матчить:
- Структурний фільтр відсіє listings з вищою ціною / іншими районами / іншим типом
- Якщо у вас є `SemanticQuery` ("тихе місце поруч з джунглями") — окремий Claude-виклик оцінить
- Listing з матчевими полями → notification → Telegram

## Семантика повідомлень

Формат HTML, з фото якщо є:
```
🏠 Новий збіг — <Назва фільтра>

💰 12,000 / міс
📍 Sri Thanu
Studio · 1 bed · 🐾 pets ok · 📶 wifi

<перші ~400 символів raw_text>

Перейти до посту →
```

Caption обмежений ~1024 символи (Telegram limit) — `RawText` обрізається.

## Multi-process архітектура

Три процеси ділять одну SQLite БД:
- `RR.McpServer` — обробляє запити з Claude Desktop, керує Locations / Sources / Filters
- `RR.Scraper.Worker` — раз/15 хв скрапить FB → AI extract → пише Listings
- `RR.TelegramBot` — раз/1 хв polling-ом забирає unprocessed Listings → match → notify

SQLite у WAL-mode (виставляється на старті кожного процесу), три писалки безпечно ділять файл.

## Типові проблеми

| Симптом | Причина | Що робити |
|---|---|---|
| `Telegram bot token не сконфігурований` | env-vars не задано | див. крок 2 |
| `Microsoft.AspNetCore.App 9.0.0 not found` | runtime mismatch (Telegram.Bot тягне 9.0) | RollForward у csproj уже виставлений — повинно працювати. Якщо ні, `dotnet add` Microsoft.AspNetCore.App 9.0 |
| Бот не відповідає на `/start` | TELEGRAM_BOT_TOKEN неправильний або бот не у тому чаті | перевір `getMe` через Telegram API |
| Notif прийшов, але фото зламане | FB CDN-урл протух | normal, photo блок просто не загрузиться — текст все одно прийде |
| Listing є в БД але notif не приходить | `ProcessedAt` уже виставлений; або не пройшов фільтр | `SELECT * FROM listings WHERE processed_at IS NULL` + перевір MatchingEngine логи |

## Що **НЕ** робить Phase 6 (відкладено на пізніше)

- Інтерактивні команди (`/pause`, `/resume`, `/filters`) — додати окремим коміттом за бажанням
- Telegram Web App / inline keyboards — overkill для personal use
- Кілька юзерів одночасно — теоретично працює (кожен пише `/start` і отримує власний chat_id), але не тестовано
