# Порівняння HTTP (лаба 5) і gRPC (лаба 6)

**Запит:** `GET /api/aggregator/orders/1/full` через Gateway.
**Методика:** `scripts/bench-aggregator.ps1`: 3 прогрівальні + 20 вимірюваних запитів, клієнтський вимір (PowerShell `Measure-Command`). Усе локально, під Aspire.

## Результати

| Варіант | avg | min | max | Викликів між сервісами на запит |
|---|---|---|---|---|
| HTTP (лаба 5), без кешу | 22,1 ms | 17,4 ms | 33,0 ms | 1 + N (Catalog) + N (Reviews) |
| gRPC, без кешу Aggregator | 16,7 ms | 14,9 ms | 21,8 ms | 3 (Orders, Catalog batch, Reviews batch) |
| gRPC + Redis-кеш Aggregator | 3,6 ms | 2,9 ms | 4,7 ms | 0 при попаданні в кеш |

Для замовлення №1 (2 позиції): HTTP робив 1 + 2 + 2 = 5 викликів, gRPC робить 3.
Середній час зменшився приблизно на 24 % (22,1 -> 16,7 ms), максимум на 34 %.

## Скріншоти

- HTTP: `baseline-http-traces.jpg`
- gRPC: `grpc-traces.jpg` (trace з gRPC-spans клієнта й сервера)
- Повторний запит без SQL-span: `cache-hit-no-sql-trace.jpg`
