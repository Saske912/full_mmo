# MMO — суперпроект (**full_mmo**)

Метарепозиторий: **`docs/`**, **`.github/`**, **[`.gitmodules`](.gitmodules)** (сабмодули `backend/`, `Unity/`).

| Репозиторий | GitHub | Назначение |
|-------------|--------|------------|
| **Суперпроект** | **[Saske912/full_mmo](https://github.com/Saske912/full_mmo)** | документация, CI, указатели submodule |
| **`backend/`** | **[Saske912/mmo](https://github.com/Saske912/mmo)** | Go (gateway, соты), Terraform, runbooks |
| **`Unity/`** | **[Saske912/Unity](https://github.com/Saske912/Unity)** | клиент Unity |

### URL для `git clone`

SSH (рекомендуется):

```bash
git clone git@github.com:Saske912/full_mmo.git MMO
cd MMO
git submodule update --init --recursive
```

HTTPS:

```bash
git clone https://github.com/Saske912/full_mmo.git MMO
cd MMO
git submodule update --init --recursive
```

Сразу с сабмодулями:

```bash
git clone --recurse-submodules git@github.com:Saske912/full_mmo.git MMO
```

### Таблица submodule (`url` в `.gitmodules`)

| Каталог | SSH | HTTPS |
|---------|-----|--------|
| `backend/` | `git@github.com:Saske912/mmo.git` | `https://github.com/Saske912/mmo.git` |
| `Unity/` | `git@github.com:Saske912/Unity.git` | `https://github.com/Saske912/Unity.git` |

В **`mmo`** — сервер; в **`Unity`** — только клиент. **Корень** пушится в **`full_mmo`**. Пуши из `backend/` и `Unity/` — в свои репозитории; после обновления закрепите коммит в суперпроекте: `git add backend Unity && git commit -m "Bump submodules"`.

Подробнее: **[`docs/README.md`](docs/README.md)**.
