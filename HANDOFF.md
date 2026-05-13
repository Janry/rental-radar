# HANDOFF — Phase 8: CI/CD polishing + optional Phase 9 (Web dashboard)

> Phase 7 (Docker + deployment) is **DONE** — artifacts готові, чекає першого реального deploy на Oracle VM.
> Phase 3b (auto-discovery) — все ще **DEFERRED** і ймовірно вже не потрібний.
> Read `CLAUDE.md` first.

## Where things stand

Усі основні фази закриті. Pipeline працює end-to-end:

```
Claude Desktop  ──→  McpServer (stdio, локально)  ──→  SQLite
                                                       ↑   ↑
Oracle Cloud:                                          │   │
  scraper container ──→ Playwright ──→ Claude ─────────┘   │
  telegram-bot container ──→ matching → TG ────────────────┘
  seq container (логи)
```

Залишилось два опційні треки.

## 🎯 Phase 8 — CI/CD polishing (optional)

Те що зараз працює "достатньо" але можна полірувати:

1. **Test coverage звіт** — `coverlet.collector` уже в xUnit dependency tree (через test SDK). Додати `--collect:"XPlat Code Coverage"` в CI test step + upload до Codecov / GitHub PR comment.

2. **Dependabot** — `.github/dependabot.yml` для автомату оновлення NuGet + Docker base images. Зменшує security drift.

3. **Renovate** як альтернатива — більш гнучкий, але і складніший.

4. **Pre-commit hook** для перевірки билду перед push'ом — `husky.net` чи raw `.git/hooks/pre-commit`.

5. **Release versioning** — `MinVer` або GitVersion, теги Docker images як `v1.2.3` замість тільки `latest`+`{sha}`.

6. **PR templates + CODEOWNERS** — поки соло-проект це overkill, але для портфоліо-сторі може мати сенс.

7. **CI image build matrix optimization** — зараз qemu emulation для arm64 повільний (~5-10 хв). Альтернатива: native ARM runner на ghcr (платний) або self-hosted на тому ж Oracle VM.

## 🎯 Phase 9 — Web dashboard (optional)

Якщо у якийсь момент захочеться UI окрім Claude Desktop і TG:

- Blazor Server чи React+REST API
- Showcase: live listings stream, filter management, source health, cost dashboard
- Read-only deploy у тому ж Docker стеку
- Auth — single-user, password у `.env` через `Microsoft.AspNetCore.Authentication.Cookies`

Корисно як portfolio piece (показує full-stack), але не критично.

## What's left to verify (на проді)

- [ ] Перший справжній deploy на Oracle VM — пройти весь `docs/DEPLOYMENT.md`
- [ ] Перевірити що ARM64 docker images крутяться (qemu build vs native ARM behavior)
- [ ] Прогнати 24-год цикл і подивитись скільки реально пожирає API, чи стабільна FB-сесія
- [ ] Cron backup перевірити після одного циклу
- [ ] Якщо FB DOM-селектори протухли — оновити константи у `FacebookScraper.cs`

## Phase 3b — на полицю

Через 1-3 місяці production-use можна повернутись. На той момент буде ясно:
- скільки реально займає manual `add_source` коли локацій більше
- чи стабільна FB anti-bot (якщо ламає manual scraper — auto-discovery теж не врятує)
- де bottleneck — у скрапінгу, AI, чи в матчінгу

Phase 3b sketch (Playwright FB-search + Claude ranker) залишається у git history.
