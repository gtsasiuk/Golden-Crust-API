# Порівняння HTTP (лаба 5) і gRPC (лаба 6)

Запит: `GET /api/aggregator/orders/1/full` через Gateway. 3 прогрівальні + 20 вимірюваних запитів, `scripts/bench-aggregator.ps1`.

| Варіант | avg | min | max | Примітка |
|---|---|---|---|---|
| HTTP (лаба 5), без кешу | 22,1 ms | 17,4 ms | 33,0 ms | baseline, клієнтський вимір |
| gRPC, без кешу Aggregator | | | | заповнимо в кроці 5 |
| gRPC + Redis-кеш Aggregator | | | | заповнимо в кроці 6 |

Скріншот baseline: `baseline-http-traces.jpg`.