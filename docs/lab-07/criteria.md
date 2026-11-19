# Лабораторна робота №7 — критерії приймання

Цей документ є навігаційною картою реалізації критеріїв лабораторної №7 у репозиторії **Golden-Crust-API**. Він доповнює основний [`README.md`](../../README.md).

> Перед здачею звірте назви класів і фактичні параметри з поточною версією коду у своїй гілці `lab-07`. Таблиця описує призначення компонентів і очікувану реалізацію; вона не замінює запуск та перевірку застосунку.

## Зведена таблиця

| № | Критерій | Вага | Основні місця реалізації |
|---:|---|---:|---|
| 1 | Standard Resilience Handler для всіх клієнтів без нашарування | 0,6 | `Platform.ServiceDefaults/Extensions.cs`, `Platform.Aggregator/` |
| 2 | Retry-політика gRPC-клієнтів за gRPC-статусами | 0,8 | `Platform.Aggregator/Program.cs`, конфігурація gRPC-клієнтів |
| 3 | Налаштовані retry, circuit breaker і таймаути | 1,1 | `Platform.Aggregator/Resilience/` |
| 4 | Різні політики для критичних і некритичних сервісів з обґрунтуванням | 0,8 | `Platform.Aggregator/Resilience/`, кореневий `README.md` |
| 5 | Graceful degradation кешу Redis | 0,8 | `Caching/` у відповідних BLL/Application, `Platform.Aggregator/`, `Platform.AppHost/Program.cs` |
| 6 | Клас бізнес-метрик з лічильниками через `IMeterFactory` | 0,9 | `Services/*/*.Api/Metrics/`, `Platform.ServiceDefaults/Extensions.cs` |
| 7 | Гістограми тривалості та gauge-метрики з низькою cardinality | 0,9 | `Services/*/*.Api/Metrics/` |
| 8 | Health checks SQL Server і MongoDB з іменами, тегами й таймаутом | 0,7 | `Services/Orders/Orders.Api/Program.cs`, `Services/Catalog/Catalog.Api/Program.cs`, `Services/Reviews/Reviews.WebApi/Program.cs` |
| 9 | Redis Health Check зі статусом `Degraded` | 0,4 | `Services/*/*Api/Program.cs`, `Platform.Aggregator/Program.cs` |
| 10 | gRPC Health Checking Protocol і перевірка залежностей в Aggregator | 0,8 | `Services/*/*Api/Program.cs`, `Platform.Aggregator/HealthChecks/`, `Platform.Aggregator/Program.cs` |
| 11 | Розділені liveness і readiness endpoints з деталями | 0,8 | `Platform.ServiceDefaults/Extensions.cs`, `Platform.ServiceDefaults/HealthCheckResponseWriter.cs` |
| 12 | Aspire AppHost показує стан ресурсів через `WithHttpHealthCheck` | 0,4 | `Platform.AppHost/Program.cs` |
| | **Разом** | **9,0** | |

## Деталізація критеріїв

### 1. Standard Resilience Handler — 0,6 бала

**Що має бути:** стандартний resilience handler підключений до HTTP-клієнтів через спільний ServiceDefaults. Не можна додавати його повторно до клієнта, якщо він уже успадковує загальні налаштування.

**Де шукати:**
- `Platform.ServiceDefaults/Extensions.cs` — `ConfigureHttpClientDefaults` та `AddStandardResilienceHandler`;
- `Platform.Aggregator/Program.cs` — реєстрація клієнтів;
- `Platform.Aggregator/Resilience/` — спеціалізовані конфігурації, якщо вони потрібні.

**Перевірка:** переглянути реєстрацію кожного клієнта й переконатися, що handlers не нашаровуються.

### 2. Retry gRPC-клієнтів за gRPC-статусами — 0,8 бала

**Що має бути:** gRPC retry policy налаштована через `ServiceConfig` / `MethodConfig` з `RetryPolicy`. У `RetryableStatusCodes` вказані статуси, які дозволено повторювати, наприклад `Unavailable` та `ResourceExhausted`.

**Де шукати:** `Platform.Aggregator/Program.cs` або окремі класи конфігурації gRPC-клієнтів.

**Перевірка:** перевірити `MaxAttempts`, `InitialBackoff`, `MaxBackoff`, `BackoffMultiplier` і список retryable status codes.

### 3. Retry, Circuit Breaker і таймаути — 1,1 бала

**Що має бути:** налаштовані кількість повторів, тип backoff, jitter, умови обробки помилок, параметри Circuit Breaker та таймаути однієї спроби й усього запиту.

**Де шукати:**
- `Platform.Aggregator/Resilience/ResilienceProfiles.cs`;
- `Platform.Aggregator/Resilience/ResilienceProfile.cs`;
- `Platform.Aggregator/Resilience/ResilienceExtensions.cs`;
- `Platform.Aggregator/Resilience/ResilienceLog.cs`.

**Перевірка:** параметри мають бути узгоджені між собою. `SamplingDuration` Circuit Breaker має відповідати вимогам бібліотеки та бути достатнім відносно таймауту спроби.

### 4. Різні політики для критичних і некритичних сервісів — 0,8 бала

**Що має бути:** політики відрізняються за критичністю залежності. Для критичного сервісу доцільно довше очікувати/повторювати, але контрольовано відмовляти; для некритичного — швидко віддавати частковий результат або fallback.

**Де шукати:** `Platform.Aggregator/Resilience/` та опис обґрунтування в кореневому `README.md`.

**Перевірка:** у документації пояснити, чому обрані параметри відповідають ролі конкретного сервісу.

### 5. Graceful degradation кешу Redis — 0,8 бала

**Що має бути:** Redis є необов'язковим кешем, а не єдиним джерелом даних. Якщо Redis недоступний, сервіс не повинен падати лише через помилку кешу; читання має переходити до доступного локального кешу та/або основної БД згідно з реалізованим сценарієм.

**Де шукати:** кеш-реалізації в `Services/Catalog/Catalog.Bll/`, `Services/Reviews/Reviews.Application/` та `Platform.Aggregator/`.

**Перевірка:** зупинити Redis і перевірити, що відповідні read-запити все ще можуть отримати дані з основного сховища. Помилки кешу мають логуватися.

### 6. Бізнес-метрики через Meter API та `IMeterFactory` — 0,9 бала

**Що має бути:** власний клас метрик використовує `IMeterFactory` для створення `Meter` і бізнес-лічильників. Назви метрик мають бути стабільними та зрозумілими.

**Де шукати:**
- `Services/Orders/Orders.Api/Metrics/`;
- `Services/Catalog/Catalog.Api/Metrics/`;
- `Services/Reviews/Reviews.WebApi/Metrics/`;
- відповідні інтерфейси в BLL/Application;
- `Platform.ServiceDefaults/Extensions.cs` — підключення meter до OpenTelemetry.

**Перевірка:** виконати бізнес-операцію й переконатися, що відповідний counter збільшується та відображається в інструментах метрик.

### 7. Histogram і Gauge з низькою cardinality — 0,9 бала

**Що має бути:** Histogram вимірює тривалість операцій, а Observable Gauge показує поточний знімок стану. Теги мають мати обмежену кількість можливих значень.

**Де шукати:** `Services/Orders/Orders.Api/Metrics/`, `Services/Catalog/Catalog.Api/Metrics/`, `Services/Reviews/Reviews.WebApi/Metrics/`.

**Перевірка:** переглянути інструменти, одиниці вимірювання та теги. Не використовувати у тегах унікальні ID замовлень, користувачів чи довільні рядки.

### 8. SQL Server і MongoDB Health Checks — 0,7 бала

**Що має бути:** перевірки відповідних баз із зрозумілими іменами, тегами та обмеженням часу. Для SQL Server достатньо легкого запиту, для MongoDB — ping.

**Де шукати:**
- `Services/Orders/Orders.Api/Program.cs`;
- `Services/Catalog/Catalog.Api/Program.cs`;
- `Services/Reviews/Reviews.WebApi/Program.cs`.

**Перевірка:** назви мають дозволяти швидко визначити несправну залежність; перевірки повинні мати тег `ready`, якщо їхній результат визначає готовність сервісу.

### 9. Redis Health Check зі статусом Degraded — 0,4 бала

**Що має бути:** `AddRedis(...)` виконує перевірку Redis (PING), має короткий timeout і `failureStatus: HealthStatus.Degraded`, оскільки кеш необов'язковий.

**Де шукати:** `Program.cs` сервісів, що використовують Redis, та реєстрація залежностей у `Platform.AppHost/Program.cs`.

**Перевірка:** зупинити Redis. Readiness має показати `Degraded`, але liveness процесу повинен залишатися `Healthy`.

### 10. gRPC Health Checking Protocol — 0,8 бала

**Що має бути:**
- на gRPC-серверах зареєстровано `AddGrpcHealthChecks()`;
- endpoint протоколу опубліковано через `MapGrpcHealthChecksService()`;
- Aggregator має власні `IHealthCheck`, які викликають стандартний `Health.HealthClient.Check`;
- виклики мають короткий deadline;
- `RpcException` обробляється та логуються.

**Де шукати:**
- `Services/Orders/Orders.Api/Program.cs`;
- `Services/Catalog/Catalog.Api/Program.cs`;
- `Services/Reviews/Reviews.WebApi/Program.cs`;
- `Platform.Aggregator/HealthChecks/`;
- `Platform.Aggregator/Program.cs`.

**Перевірка:** зупинити один із downstream gRPC-сервісів і перевірити, що відповідна перевірка в Aggregator стає `Unhealthy`, а після відновлення сервісу повертається до `Healthy`.

### 11. Liveness і Readiness з деталями — 0,8 бала

**Що має бути:**
- `/health/live` перевіряє лише сам процес;
- `/health/ready` перевіряє залежності, потрібні для обслуговування запитів;
- readiness повертає деталізовану JSON-відповідь;
- перевірки відбираються за тегами через `Predicate`.

**Де шукати:**
- `Platform.ServiceDefaults/Extensions.cs`;
- `Platform.ServiceDefaults/HealthCheckResponseWriter.cs`.

**Перевірка:**
- при зупиненому Redis liveness залишається `Healthy`, readiness може бути `Degraded`;
- при зупиненій критичній БД liveness залишається `Healthy`, readiness стає `Unhealthy`.

### 12. Aspire AppHost та WithHttpHealthCheck — 0,4 бала

**Що має бути:** для HTTP-сервісів AppHost вказує endpoint перевірки, наприклад `.WithHttpHealthCheck("/health")`. Dashboard відображає стан ресурсу за результатом health check.

**Де шукати:** `Platform.AppHost/Program.cs`.

**Перевірка:** запустити AppHost, відкрити Aspire Dashboard → Resources і перевірити, що для сервісів відображаються health status. Інфраструктурні контейнери можуть мати власні health checks Aspire; HTTP health check застосовується до HTTP-проєктів.

## Підсумкова перевірка перед здачею

- [ ] `dotnet restore` завершується успішно.
- [ ] `dotnet build` завершується без помилок.
- [ ] HTTP resilience не дублюється на клієнтах.
- [ ] gRPC retry налаштований саме за gRPC-статусами.
- [ ] Параметри retry, breaker і timeout задокументовані.
- [ ] Відмова Redis не зупиняє читання з основного сховища.
- [ ] Counter, Histogram і Gauge з'являються після виконання відповідних операцій.
- [ ] SQL Server, MongoDB та Redis мають health checks з правильними статусами.
- [ ] gRPC Health Check працює для downstream-сервісів.
- [ ] `/health/live` і `/health/ready` поводяться по-різному під час відмов залежностей.
- [ ] Aspire Dashboard відображає health status HTTP-сервісів.
- [ ] У логах видно події Retry та Circuit Breaker.

## Демонстраційні матеріали

Методичка рекомендує зберегти такі приклади для демонстрації:

1. Подія `OnRetry` та перехід Circuit Breaker (`OnCircuitOpened` або відповідний лог).
2. Власна бізнес-метрика після виконання операції.
3. `/health/ready` зі статусом `Degraded` при зупиненому Redis.
4. Aspire Dashboard → Resources зі станами ресурсів.

Скріншоти є підтвердженням демонстрації, але основним доказом реалізації залишається код і відтворювані результати перевірок.
