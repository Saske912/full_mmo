# Документация MMO

**Суперпроект:** **[github.com/Saske912/full_mmo](https://github.com/Saske912/full_mmo)** — в корне **`docs/`**, **`.github/`**; **[`backend/`](../backend/)** и **[`Unity/`](../Unity/)** — **[submodules](https://git-scm.com/book/en/v2/Git-Tools-Submodules)** (`mmo` + `Unity`).

Клонирование: **`git clone git@github.com:Saske912/full_mmo.git`** → **`git submodule update --init --recursive`** (или **`clone --recurse-submodules`**).

URL сабмодулей — в **[`.gitmodules`](../.gitmodules)**; команды и таблица remotes — в **[`README.md`](../README.md)**.

## Оглавление

| Документ | Назначение |
|----------|------------|
| [roadmap-checklist.md](roadmap-checklist.md) | Roadmap, снимок состояния стека, Phase 0–4, ручная проверка playground |
| [webgl-staging-e2e.md](webgl-staging-e2e.md) | WebGL: билд, HTTPS/wss, smoke против staging, типичные ошибки |
| [crypto-economy.md](crypto-economy.md) | Криптоэкономика BET, NFT, чеклисты внедрения §2 |
| [bet-whitepaper-v0.md](bet-whitepaper-v0.md) | Whitepaper v0: utility BET, burn, NFT, фазы A–H |
| [web3-implementation-tracker.md](web3-implementation-tracker.md) | Web3: очередность A–H, привязка к §2, статус в репо |
| [phase0-foundation-backlog.md](phase0-foundation-backlog.md) | Phase 0: сводка открытого техдолга (ссылки на роадмап) |
| [ci-and-deploy.md](../backend/docs/ci-and-deploy.md) | CI GitHub Actions, деплой staging, проверка goose после выката |
| [cells-migration-workflow.md](../backend/docs/cells-migration-workflow.md) | Указатель: migration-dry-run, handoff, §7–8 runbook |
| [stack-design-notes.md](../backend/docs/archive/stack-design-notes.md) | Архив: исторический черновик стека (**не** канон кода) |

## Runbooks и observability

- **Cold-path / split сот:** [`../backend/runbooks/cold-cell-split.md`](../backend/runbooks/cold-cell-split.md)
- **Примеры дашбордов и правил:** [`../backend/deploy/observability/`](../backend/deploy/observability/)
