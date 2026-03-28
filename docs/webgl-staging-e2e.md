# WebGL: проверка против staging

Краткий чеклист для билда **WebGL** с доступом к gateway по **https** и мировому сокету по **wss** (тот же хост, что в [`MmoGatewayClient.BuildWebSocketUri`](../Unity/Assets/MMO/Runtime/MmoGatewayClient.cs)).

## Сборка

1. В Unity: **File → Build Settings → WebGL → Switch Platform**.
2. **Player Settings:** для запросов к API разрешите домен staging (если используете ограничения Unity) или тестовый хостинг с тем же origin-политикой, что и у страницы с игрой.
3. Соберите билд и разместите на **HTTPS** (статический хост или локальный dev-server с TLS). Смешанный контент (**http**-страница → **wss**) браузер обычно блокирует.

## Параметры Playground (`MmoGameBootstrap`)

- **Адрес gateway:** например `https://mmo.pass-k8s.ru` (как в снимке [roadmap-checklist.md](roadmap-checklist.md)).
- **player_id:** уникальный на время теста.
- **resolve_x / resolve_z:** `0,0` или координаты дочерней соты (например `-500,-500` для child-sw), согласованные с каталогом.

## Сценарий e2e

1. Открыть игру в браузере, консоль **DevTools** (вкладка Console / Network).
2. **Подключиться к миру** — без ошибок CORS/TLS; в Network виден upgrade **`/v1/ws`** (**wss**).
3. Проверить **WASD**, отображение меты (золото, инвентарь, квесты).
4. **Инвентарь:** клик по строке → поле «Предмет (удалить)» → **Удалить 1 шт.** — мета обновляется после успешного запроса.
5. **409 / handoff:** при смене соты — реконнект по сценарию из [cold-cell-split runbook](../backend/runbooks/cold-cell-split.md) и полей **last_cell** на bootstrap.

## Типичные проблемы

- **WebSocket failed:** проверить, что базовый URL — **https**, не **http**; что хост не режет длинные query с `token`.
- **HTTP к REST:** на WebGL используется **`MmoGatewayClient.WebGL.cs`** (**UnityWebRequest**). При несовместимости с версией Unity сверить с веткой **Http** в Editor.

## См. также

- Ручные шаги в разделе «Клиент Playground» в [roadmap-checklist.md](roadmap-checklist.md).
- Деплой backend: [ci-and-deploy.md](../backend/docs/ci-and-deploy.md).
