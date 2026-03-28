# Документация MMO

**Суперпроект:** **[github.com/Saske912/full_mmo](https://github.com/Saske912/full_mmo)** — в корне **`docs/`**, **`.github/`**; **[`backend/`](../backend/)** и **[`Unity/`](../Unity/)** — **[submodules](https://git-scm.com/book/en/v2/Git-Tools-Submodules)** (`mmo` + `Unity`).

Клонирование: **`git clone git@github.com:Saske912/full_mmo.git`** → **`git submodule update --init --recursive`** (или **`clone --recurse-submodules`**).

URL сабмодулей — в **[`.gitmodules`](../.gitmodules)**; команды и таблица remotes — в **[`README.md`](../README.md)**.

## Оглавление

| Документ | Назначение |
|----------|------------|
| [roadmap-checklist.md](roadmap-checklist.md) | Roadmap, снимок состояния стека, Phase 0–4, ручная проверка playground |
| [crypto-economy.md](crypto-economy.md) | Криптоэкономика BET, NFT, очередность внедрения |
| [ci-and-deploy.md](../backend/docs/ci-and-deploy.md) | CI GitHub Actions, деплой staging, проверка goose после выката |
| [webgl-staging-e2e.md](webgl-staging-e2e.md) | WebGL: https/wss, чеклист против staging |

## Runbooks и observability

- **Cold-path / split сот:** [`../backend/runbooks/cold-cell-split.md`](../backend/runbooks/cold-cell-split.md)
- **Примеры дашбордов и правил:** [`../backend/deploy/observability/`](../backend/deploy/observability/)
