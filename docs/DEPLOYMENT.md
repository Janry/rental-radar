# Deployment (Phase 7)

Production target: **Oracle Cloud Always Free** — 4 ARM (Ampere) vCPU + 24 GB RAM. Безкоштовно безстроково (доки Oracle тримає програму).

Стек у Docker:
- `scraper` — RR.Scraper.Worker + Playwright Chromium
- `telegram-bot` — RR.TelegramBot
- `seq` — централізовані логи (доступ через SSH-tunnel)

`McpServer` НЕ деплоїться — живе на ноуті користувача поруч з Claude Desktop (stdio constraint).

---

## 1. Створити Oracle Cloud Always Free VM

1. Зареєструвати акаунт на [oracle.com/cloud/free](https://www.oracle.com/cloud/free/)
2. Compute → Create Instance:
   - Name: `rental-radar`
   - Image: **Ubuntu 22.04**
   - Shape: **VM.Standard.A1.Flex** (ARM64 Ampere) — 4 OCPU, 24 GB RAM (Always Free max)
   - Network: створити VCN, отримати public IP
   - SSH keys: завантажити свій public key
3. Дочекатися статусу Running

> **Чому ARM?** Always Free дає ARM Ampere безкоштовно. AMD x86 теж є але тільки 2 micro-instance (1/8 OCPU кожен) — мало для нашого стеку.

### Network rules
- Open ingress на port 22 (SSH) — за замовчуванням так
- Port 5341 (Seq) — **НЕ** відкривати публічно, тільки через SSH-tunnel

## 2. Підготувати VM

```bash
ssh ubuntu@<public-ip>

# Docker
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker ubuntu
exit; ssh ubuntu@<public-ip>   # перезайти щоб група застосувалась

# Структура каталогів
sudo mkdir -p /opt/rental-radar/{data,backups}
sudo chown -R ubuntu:ubuntu /opt/rental-radar
cd /opt/rental-radar

# Витягнути docker/ і docs/ з репо (clone або scp)
git clone https://github.com/Janry/rental-radar.git .
```

## 3. Конфіги і секрети

```bash
cp .env.example .env
nano .env   # заповнити:
#   ANTHROPIC_API_KEY=sk-ant-api03-...
#   TELEGRAM_BOT_TOKEN=123456:ABC...
#   FACEBOOK_SESSION_PATH=/app/data/fb-session.json
#   ConnectionStrings__Default=Data Source=/app/data/rental_radar.db;Cache=Shared;Foreign Keys=True
```

## 4. FB session (одноразово)

Запускати **локально** (бо потрібен головний браузер для логіну) і відправити на VM:

```bash
# Local (Windows)
dotnet run --project tools/FbLogin -- ./fb-session.json

# Upload
scp fb-session.json ubuntu@<public-ip>:/opt/rental-radar/data/
```

## 5. Перший прогін БД-міграції

```bash
# На VM
docker compose -f docker/docker-compose.yml run --rm scraper \
    dotnet ef database update --no-build
# або просто перший запуск worker-а застосує міграції автоматично якщо
# додали такий хук (поки що ні — застосовуйте руками)
```

## 6. Підняти стек

```bash
cd /opt/rental-radar
# ⚠️ ghcr.io образи зараз публікуються тільки для linux/amd64 (CI не вмиє ARM
# через QEMU emulation). На ARM-VM Oracle треба збирати локально — нативний
# ARM build швидкий, ~5-15 хв перший раз, потім layer cache.
docker compose -f docker/docker-compose.yml build
docker compose -f docker/docker-compose.yml up -d

# Перевірка
docker compose -f docker/docker-compose.yml ps
docker compose -f docker/docker-compose.yml logs -f
```

> Якщо колись CI почне публікувати arm64 (через native ARM runner або self-hosted),
> замість `build` буде `pull` як у звичайному ghcr.io workflow.

## 7. Логи через Seq

Seq UI слухає `5341` всередині docker network, **не** експонується назовні.

SSH-tunnel з ноута:
```bash
ssh -L 5341:localhost:5341 ubuntu@<public-ip>
# Залишити термінал відкритим
```

В браузері: http://localhost:5341 — звичайний Seq UI з логами обох воркерів.

## 8. Backup SQLite (cron)

```bash
sudo crontab -e
# додати:
0 3 * * * /opt/rental-radar/docker/backup-sqlite.sh >> /var/log/rr-backup.log 2>&1
```

Backups лежатимуть у `/opt/rental-radar/backups/rental_radar_YYYYMMDD_HHMMSS.db.gz`. Стандартно тримаємо 14 днів (env var `RR_KEEP_DAYS`).

Для off-site: додати у скрипт rsync до Backblaze B2 (10 GB free) або іншого S3-сумісного. Phase 8+.

## 9. Оновлення стеку

Push до main → GitHub Actions білдить multi-arch образи (amd64 + arm64) і пушить до ghcr.io.

На VM:
```bash
cd /opt/rental-radar && git pull   # pull docker-compose.yml updates
docker compose -f docker/docker-compose.yml pull
docker compose -f docker/docker-compose.yml up -d
```

Restart "rolling": кожен container перестворюється з новим image, ~10-30 c downtime.

## 10. Коли FB session запротухне

Симптом у Seq: `FB redirected to login wall for ... Сесія застаріла`.

Дія:
```bash
# Local
dotnet run --project tools/FbLogin -- ./fb-session-new.json
scp fb-session-new.json ubuntu@<public-ip>:/opt/rental-radar/data/fb-session.json
# Restart scraper
ssh ubuntu@<public-ip> "cd /opt/rental-radar && docker compose -f docker/docker-compose.yml restart scraper"
```

## 11. Troubleshooting

| Симптом | Що перевірити |
|---|---|
| `pull access denied` | Очікувано — packages у ghcr.io створюються private. Не fix-ити: ми build-имо локально на VM (крок 6). `docker compose pull` пропускаємо |
| `exec format error` | amd64 image на arm64 host. Запустіть `docker compose build` (а не `pull`) — це створить native arm64 образи |
| TG не приходить | `docker compose logs telegram-bot` — перевір TELEGRAM_BOT_TOKEN, chat_id у filter |
| Disk space | `docker system prune -a`; backups можна теж пом'якшити через RR_KEEP_DAYS |

## Cost summary

- **Oracle Cloud Always Free VM**: $0/міс
- **Anthropic API** (Haiku 4.5): ~$30/міс на типовому навантаженні (див. AI_EXTRACTION.md)
- **GitHub Container Registry**: $0 (ми не покладаємось на pull — build-имо на VM)
- **Telegram Bot API**: $0
- **SQLite + storage**: $0

Total: ~$30/міс — повністю на Claude API.
