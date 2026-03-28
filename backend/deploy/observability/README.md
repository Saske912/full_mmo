# Observability (MMO backend)

## Трассировка тиков соты

Для разбора задержек RPC/tick включите на **cell-node**:

- `MMO_CELL_OTEL_TICK_SPAN=1` — точечные span’ы на тиках (дороже по объёму; использовать при расследованиях).

## Grafana

- Дашборд **grid RPC p95 по методам**: импорт JSON [`grafana-dashboard-grid-rpc-p95-by-method.json`](grafana-dashboard-grid-rpc-p95-by-method.json). После смены инстанса Grafana файл нужно **реимпортировать** или обновить через GitOps.

## Примеры

- [`prometheus-rule-forward-npc-handoff.example.yaml`](prometheus-rule-forward-npc-handoff.example.yaml)
- [`loki-logql-traceid.example.txt`](loki-logql-traceid.example.txt)
