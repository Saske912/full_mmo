# Observability (MMO backend)

## Трассировка тиков соты

Для разбора задержек RPC/tick включите на **cell-node**:

- `MMO_CELL_OTEL_TICK_SPAN=1` — точечные span’ы на тиках (дороже по объёму; использовать при расследованиях).

## Grafana

- Дашборд **grid RPC p95 по методам**: импорт JSON [`grafana-dashboard-grid-rpc-p95-by-method.json`](grafana-dashboard-grid-rpc-p95-by-method.json). UID дашборда: **`mmo-grid-rpc-p95`**. После смены инстанса Grafana файл нужно **реимпортировать** или обновить через GitOps.

### Staging / после выката

- При новом Grafana: импорт JSON вручную или через провайдера — иначе панели по **`mmo_grid_registry_rpc_*`** не появятся.
- **`MMO_CELL_OTEL_TICK_SPAN`:** держите **выключенным** по умолчанию на cell-node; включайте **точечно** при расследовании задержек тика (рост объёма span’ов в бэкенде сохранения).

## Примеры

- [`prometheus-rule-forward-npc-handoff.example.yaml`](prometheus-rule-forward-npc-handoff.example.yaml)
- [`loki-logql-traceid.example.txt`](loki-logql-traceid.example.txt)
