# WebGL: проверка против staging (HTTPS + WSS)

Краткий чеклист **e2e** клиента Unity на **WebGL** с gateway **`https://mmo.pass-k8s.ru`** (или другой URL из Ingress). Соответствует шагу 8 снимка в [roadmap-checklist.md](roadmap-checklist.md).

### Факт проверки (staging)

**2026-03-29:** ручной прогон WebGL-билда — подключение к миру, **`resolve_x` / `resolve_z` = -500 / -500** (сота **`cell_-1_-1_1`**), прохождение шагов квеста, удаление предмета из инвентаря — **OK** (оператор).

## 1. Настройки билда

- **Build Settings:** платформа **WebGL**, **Compression** по возможности **Brotli** (хост должен отдавать корректные `Content-Type` / заголовки, как требует Unity).
- В сцене **MmoPlayground** у **MmoGameBootstrap** поле базового URL (**`_defaultBaseUrl`**) = **`https://mmo.pass-k8s.ru`** (уже дефолт в репозитории) или ваш стенд.
- Транспорт мира: **`MmoWorldStreamClient`** при `UNITY_WEBGL` → **`MmoWebSocket.jslib`**, **`Poll()`** вызывается из **`Update`** (см. `MmoGameBootstrap`).

## 2. Хостинг билда

- Страница открывается по **HTTPS** (браузер блокирует небезопасный контекст и смешанный контент).
- WebSocket к gateway должен быть **wss://** (клиент строит URL от той же схемы, что и REST — при `https://` получится **wss**).

## 3. Ручная проверка (smoke)

1. Открыть билд в браузере, консоль разработчика (**F12**).
2. В UI: **`player_id`** — **уникальный** на сессию (иначе коллизии с другими тестами). **`resolve_x` / `resolve_z`:** для дочерней соты **`cell_-1_-1_1`** задайте **`-500`** и **`-500`** (см. снимок [roadmap-checklist.md](roadmap-checklist.md)); для родителя **`0` / `0`** подходит.
3. **Подключиться к миру** (кнопка) — REST-сессия и мета (золото, инвентарь, квесты); затем WebSocket без ошибок в консоли.
4. **WASD** — движение куба; при обрыве сети — реконнект / handoff как в [cold-cell-split.md](../backend/runbooks/cold-cell-split.md).
5. **Инвентарь:** клик по **строке** списка под метой — строка **подсвечивается**, поле «Предмет (удалить)» заполняется → **«Удалить 1 шт.»**; мета обновляется после ответа API.
6. **Квест:** id из меты (например **`tutorial_intro`**) → **«Шаг прогресса квеста»** до **`target_progress`** (часто **3** для tutorial); **`progress`** на сервере **абсолютный** — клиент шлёт следующий шаг из меты сессии.
7. **409 / handoff:** при смене соты сценарий **last_cell** в **`MmoGameBootstrap`** — см. runbook; при необходимости новая сессия с координатами из gateway.
8. Контроль: в консоли нет красных ошибок по **CORS**, **WS**, **SSL**; при сомнениях сравните с шагами 1–8 снимка в [roadmap-checklist.md](roadmap-checklist.md).

## 4. Типичные проблемы

| Симптом | Что проверить |
|--------|----------------|
| CORS / блокировка запросов | Gateway и Ingress должны разрешать origin вашего хоста статики (если API на другом домене — CORS на gateway). |
| WS не поднимается | **wss**, корректный путь **`/v1/ws`**, таймауты Ingress (в Terraform уже увеличены proxy-read/send timeout). |
| HTTP из WebGL падает, в Editor ок | В WebGL **`HttpClient`** недоступен; используется **`UnityWebRequest`** в **`MmoGatewayClient.WebGL.cs`** — сверить версию Unity и логи. |
| SSL | Доверенный сертификат на gateway; для самоподписанного локального стенда браузер должен доверять цепочке. |

## 5. Полировка UX (опционально)

- **Инвентарь:** в Playground уже есть **подсветка** выбранной строки и обновление меты после **quest-progress** / **items/remove** — см. **`MmoGameBootstrap`** (submodule **`Unity/`**).
- **Движение:** в инспекторе **`MmoGameBootstrap`** — **Position Extrapolation** / **Max Extrapolation Delta** / **Position Smoothing**; сервер по-прежнему авторитетен.

## См. также

- [roadmap-checklist.md](roadmap-checklist.md) — клиент Playground, шаги 1–8.
- [backend/docs/ci-and-deploy.md](../backend/docs/ci-and-deploy.md) — выкат сервера и `staging-verify`.
