# WebGL: проверка против staging (HTTPS + WSS)

Краткий чеклист **e2e** клиента Unity на **WebGL** с gateway **`https://mmo.pass-k8s.ru`** (или другой URL из Ingress). Соответствует шагу 8 снимка в [roadmap-checklist.md](roadmap-checklist.md).

## 1. Настройки билда

- **Build Settings:** платформа **WebGL**, **Compression** по возможности **Brotli** (хост должен отдавать корректные `Content-Type` / заголовки, как требует Unity).
- В сцене **MmoPlayground** у **MmoGameBootstrap** поле базового URL (**`_defaultBaseUrl`**) = **`https://mmo.pass-k8s.ru`** (уже дефолт в репозитории) или ваш стенд.
- Транспорт мира: **`MmoWorldStreamClient`** при `UNITY_WEBGL` → **`MmoWebSocket.jslib`**, **`Poll()`** вызывается из **`Update`** (см. `MmoGameBootstrap`).

## 2. Хостинг билда

- Страница открывается по **HTTPS** (браузер блокирует небезопасный контекст и смешанный контент).
- WebSocket к gateway должен быть **wss://** (клиент строит URL от той же схемы, что и REST — при `https://` получится **wss**).

## 3. Ручная проверка (smoke)

1. Открыть билд в браузере, консоль разработчика (**F12**).
2. Старт сцены: сессия (**POST** session), мета (инвентарь, квесты).
3. **Подключиться к миру** — WS без ошибок в консоли.
4. **WASD** — движение; при обрыве сети — ожидаемое поведение реконнекта (см. handoff в [cold-cell-split.md](../backend/runbooks/cold-cell-split.md)).

## 4. Типичные проблемы

| Симптом | Что проверить |
|--------|----------------|
| CORS / блокировка запросов | Gateway и Ingress должны разрешать origin вашего хоста статики (если API на другом домене — CORS на gateway). |
| WS не поднимается | **wss**, корректный путь **`/v1/ws`**, таймауты Ingress (в Terraform уже увеличены proxy-read/send timeout). |
| HTTP из WebGL падает, в Editor ок | В WebGL **`HttpClient`** недоступен; используется **`UnityWebRequest`** в **`MmoGatewayClient.WebGL.cs`** — сверить версию Unity и логи. |
| SSL | Доверенный сертификат на gateway; для самоподписанного локального стенда браузер должен доверять цепочке. |

## 5. Полировка UX (опционально)

- **Инвентарь:** клик по строке в Playground → удаление предмета (сценарий в чеклисте).
- **Движение:** при необходимости добавить **prediction** на клиенте (отдельная задача; сервер по-прежнему авторитетен).

## См. также

- [roadmap-checklist.md](roadmap-checklist.md) — клиент Playground, шаги 1–8.
- [backend/docs/ci-and-deploy.md](../backend/docs/ci-and-deploy.md) — выкат сервера и `staging-verify`.
