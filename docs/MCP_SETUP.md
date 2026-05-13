# Підключення RentalRadar MCP-сервера до Claude Desktop

> Цей документ описує локальну розробку. Production-deploy буде в Phase 7.

## 1. Підготовка

```bash
# Відновити локальні tools (dotnet-ef)
dotnet tool restore

# Зібрати solution
dotnet build RentalRadar.slnx
```

## 2. Створити локальну БД

SQLite файл лежить у `src/RR.McpServer/data/rental_radar.db` (відносно cwd при запуску). Папка створюється автоматично через міграцію, але на першому запуску її варто створити вручну якщо її ще нема.

```bash
mkdir -p src/RR.McpServer/data
dotnet ef database update --project src/RR.Infrastructure --startup-project src/RR.McpServer
```

Перевірити, що схема створилась:

```bash
sqlite3 src/RR.McpServer/data/rental_radar.db ".tables"
# Очікувано: __EFMigrationsHistory  listings  locations  scrape_sources  user_filters
```

## 3. Підключити Claude Desktop

Знайти `claude_desktop_config.json`:
- **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
- **macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`

Додати запис `rental-radar` у `mcpServers`:

```json
{
  "mcpServers": {
    "rental-radar": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "D:/Repos/Janry/rental-radar/src/RR.McpServer",
        "--no-build"
      ],
      "env": {
        "ConnectionStrings__Default": "Data Source=D:/Repos/Janry/rental-radar/src/RR.McpServer/data/rental_radar.db;Cache=Shared;Foreign Keys=True"
      }
    }
  }
}
```

> Шлях у `--project` має бути абсолютним. `--no-build` пропускає рекомпіляцію — спочатку завжди run `dotnet build` руками. Якщо хочете щоб Claude Desktop сам білдив — приберіть прапор, але запуск буде довшим.

Перезапустіть Claude Desktop.

## 4. Перевірка end-to-end

У новому чаті Claude Desktop:

> *Додай Ko Phangan у RentalRadar, країна TH, райони Sri Thanu, Tong Sala, валюта THB.*

Очікувано: Claude викличе `add_location`, повернеться JSON з `location_id`, `discovered_sources: []` (поки що stub до Phase 3).

Перевірити в БД:

```bash
sqlite3 src/RR.McpServer/data/rental_radar.db "SELECT name, slug, country, currency, areas FROM locations;"
```

Потім:

> *Покажи всі локації, які я моніторю.*

Очікувано: повертає список з однією локацією.

## 5. Логи

Якщо щось не працює — MCP-сервери пишуть в **stderr** (stdout — це JSON-RPC канал, його чіпати не можна). Claude Desktop логи:
- **Windows**: `%APPDATA%\Claude\logs\mcp*.log`
- **macOS**: `~/Library/Logs/Claude/mcp*.log`

## 6. Часті проблеми

- **`SQLite Error 14: unable to open database file`** — папка `data/` не існує там, де cwd процесу. Зробіть `mkdir -p` за абсолютним шляхом і вкажіть його в `ConnectionStrings__Default`.
- **`ConnectionStrings:Default is not configured`** — `appsettings.json` не скопіювався у bin. Перевірте `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>` у csproj, або задайте через env var `ConnectionStrings__Default`.
- **MCP server не запускається в Claude Desktop** — спочатку запустіть командний рядок руками: `dotnet run --project src/RR.McpServer`. Сервер має не падати протягом 2-3 секунд (stdio чекає на JSON-RPC від клієнта).
