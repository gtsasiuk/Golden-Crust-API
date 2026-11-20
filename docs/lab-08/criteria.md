# Лаб. 08: карта критеріїв і доказів

Цей документ фіксує, де в репозиторії **Golden-Crust-API** реалізований кожен із 13 критеріїв Лаб. 08 і чим його можна підтвердити. Шляхи вказані від кореня репозиторію. Назви класів і методів стабільніші за номери рядків.

> Статуси нижче відображають аналіз вихідного коду ZIP-архіву. Вони не означають, що всі сценарії були виконані на запущеній інфраструктурі. Перед здачею виконайте локальні команди та практичні перевірки.

## Швидка перевірка

```powershell
dotnet restore .\Platform.slnx
dotnet build .\Platform.slnx --nologo
dotnet run --project .\Platform.AppHost
```

Очікування для build: 0 помилок. У наданому архіві окремих test projects не виявлено, тому очікування на кшталт «10/10 тестів» до цього ZIP не застосовується. Перевірте наявність тестів у своїй робочій гілці, якщо вони додавалися окремо.

## Зведена таблиця

| № | Критерій | Вага | Стан за кодом |
|---:|---|---:|---|
| 1 | RabbitMQ в AppHost з volume і Management UI | 0,6 | Реалізовано |
| 2 | Підключення через Aspire.RabbitMQ.Client і RabbitMQ.Client 7.x | 0,4 | Реалізовано |
| 3 | Декларативна топологія при старті: topic exchange, черги, bindings, DLX/DLQ | 1,1 | Реалізовано |
| 4 | Незмінні integration events з EventId і CorrelationId | 0,8 | Реалізовано для подій зміни ціни та перейменування |
| 5 | IEventPublisher з persistent-публікацією в JSON | 1,0 | Реалізовано |
| 6 | Події публікуються лише після успішного збереження змін | 0,8 | Реалізовано для UpdateAsync і ChangePriceAsync |
| 7 | Consumer як BackgroundService з ручним підтвердженням | 1,0 | Реалізовано |
| 8 | ACK після успіху, NACK без requeue для постійних збоїв | 0,8 | Реалізовано |
| 9 | Ідемпотентні обробники з журналом оброблених подій | 1,1 | Реалізовано; у Reviews без спільної MongoDB-транзакції |
| 10 | Реплікація копій даних за політикою Лаб. 01 | 1,1 | Реалізовано для перейменування та зміни ціни |
| 11 | Повтори з лічильником x-retry-count і максимумом спроб | 0,9 | Реалізовано; без затримки/backoff |
| 12 | Передача контексту трасування через брокер | 1,0 | Реалізовано на рівні коду |
| 13 | Структуроване логування збоїв із бізнес-контекстом | 0,4 | Реалізовано |

**Разом: 11,0 бала.**

## Критерії

### 1. RabbitMQ в AppHost з volume і Management UI — 0,6 бала

**Де:**

- `Platform.AppHost/Program.cs`, ресурс `messaging`:
  - `builder.AddRabbitMQ("messaging")`;
  - `.WithDataVolume()`;
  - `.WithManagementPlugin(port: 15672)`;
  - `catalog-api`, `orders-api`, `reviews-api` отримують `.WithReference(messaging)` і `.WaitFor(messaging)`.
- `Platform.AppHost/Platform.AppHost.csproj`: `Aspire.Hosting.RabbitMQ` 13.6.1.

**Перевірка:** запустити `dotnet run --project .\Platform.AppHost`; у Aspire Dashboard має бути ресурс `messaging`. Відкрити Management UI через endpoint/порт 15672, який показує Aspire.

### 2. Підключення через Aspire.RabbitMQ.Client і RabbitMQ.Client 7.x — 0,4 бала

**Де:**

- `Services/Catalog/Catalog.Api/Catalog.Api.csproj`: `Aspire.RabbitMQ.Client` 13.6.1.
- `Services/Orders/Orders.Api/Orders.Api.csproj`: `Aspire.RabbitMQ.Client` 13.6.1.
- `Services/Reviews/Reviews.WebApi/Reviews.WebApi.csproj`: `Aspire.RabbitMQ.Client` 13.6.1.
- `Platform.EventBus/Platform.EventBus.csproj`: `RabbitMQ.Client` 7.1.2.
- `Program.cs` усіх трьох сервісів: `builder.AddRabbitMQClient("messaging")`.

**Перевірка:** після запуску AppHost усі три сервіси мають підключитися до ресурсу `messaging`; перевірити логи старту consumers і стан ресурсів у Dashboard. Реєстрацію клієнта здійснює Aspire integration.

### 3. Декларативна топологія при старті — 1,1 бала

**Де:**

- `Platform.EventBus/Topology/MessagingConstants.cs` — централізовані назви exchanges, queues та routing keys.
- `Platform.EventBus/Topology/RabbitMqTopologyHostedService.cs`, `StartAsync`:
  - durable topic exchange `catalog-events`;
  - durable topic exchange `orders-events` (зарезервований);
  - durable direct exchange `catalog-events.dlx`;
  - durable queues `orders.catalog-sync` і `reviews.catalog-sync`;
  - DLQ `orders.catalog-sync.dlq` і `reviews.catalog-sync.dlq`;
  - `x-dead-letter-exchange` та `x-dead-letter-routing-key` на робочих чергах;
  - binding обох робочих черг до `catalog-events` з routing key `product.*`.
- `Platform.EventBus/DependencyInjection/MessagingExtensions.cs`: `AddMessagingTopology()` реєструє `RabbitMqTopologyHostedService`.
- `Program.cs` Catalog, Orders і Reviews викликає `AddMessagingTopology()`.

**Перевірка:** RabbitMQ Management UI → Exchanges і Queues. Переконатися, що `catalog-events` має тип topic, `catalog-events.dlx` — direct, а обидві робочі черги мають DLX-аргументи й bindings. Повідомлення з routing keys `product.price_changed` і `product.renamed` відповідають `product.*`.

### 4. Незмінні integration events з EventId і CorrelationId — 0,8 бала

**Де:**

- `Platform.EventBus/Contracts/IntegrationEvent.cs`: `abstract record` з `EventId`, `OccurredAt`, `CorrelationId`, `SchemaVersion`.
- `Platform.EventBus/Contracts/MenuItemPriceChangedEvent.cs`: `sealed record` із `MenuItemId`, `MenuItemName`, `OldPrice`, `NewPrice`.
- `Platform.EventBus/Contracts/MenuItemRenamedEvent.cs`: `sealed record` із `MenuItemId`, `OldName`, `NewName`.
- `Services/Catalog/Catalog.Bll/Services/MenuItemService.cs`: створення подій у `UpdateAsync` і `ChangePriceAsync`.

**Перевірка:** у RabbitMQ Management UI переглянути повідомлення та properties: `message_id` відповідає `EventId`, `CorrelationId` передається у властивостях повідомлення, а також у заголовку `x-correlation-id`.

**Обсяг:** у поточному коді є події зміни ціни й перейменування. Події створення та видалення товару не реалізовані.

### 5. IEventPublisher з persistent-публікацією в JSON — 1,0 бала

**Де:**

- `Platform.EventBus/Publishing/IEventPublisher.cs`: контракт `PublishAsync`.
- `Platform.EventBus/Publishing/RabbitMqEventPublisher.cs`, `PublishAsync`:
  - `Persistent = true`;
  - `ContentType = "application/json"`, `ContentEncoding = "utf-8"`;
  - `MessageId = EventId`;
  - `Type = typeof(T).Name`;
  - `CorrelationId`;
  - заголовки `x-event-type`, `x-schema-version`, `x-occurred-at`;
  - JSON-серіалізація через `JsonSerializer.SerializeToUtf8Bytes` і `JsonDefaults.Options`.
- `Platform.EventBus/Tracing/TracePropagation.cs`, `Inject`: додає W3C trace context до headers, якщо активний Activity існує.

**Перевірка:** лог `Published event ...` у Catalog API; у Management UI перевірити delivery mode/persistent, content type, message id, type і headers.

**Обмеження:** Publisher Confirms у поточній реалізації не налаштовані. Успішне завершення `BasicPublishAsync` не слід трактувати як підтвердження брокера про надійне прийняття повідомлення.

### 6. Події публікуються лише після успішного збереження змін — 0,8 бала

**Де:** `Services/Catalog/Catalog.Bll/Services/MenuItemService.cs`.

- `UpdateAsync`: змінює сутність → `uow.SaveChangesAsync(ct)` → за потреби публікує `MenuItemRenamedEvent` та/або `MenuItemPriceChangedEvent`.
- `ChangePriceAsync`: змінює ціну → `uow.SaveChangesAsync(ct)` → публікує `MenuItemPriceChangedEvent`.

**Перевірка:** простежити послідовність викликів у цих методах. Якщо `SaveChangesAsync` завершується винятком, до `PublishAsync` виконання не доходить.

**Обмеження:** Outbox pattern не реалізований. Якщо БД уже зберегла зміни, але подальша публікація не вдалася, атомарного відновлення події немає.

### 7. Consumer як BackgroundService з ручним підтвердженням — 1,0 бала

**Де:**

- `Services/Orders/Orders.Api/IntegrationEvents/OrdersCatalogEventsConsumer.cs`;
- `Services/Reviews/Reviews.WebApi/IntegrationEvents/ReviewsCatalogEventsConsumer.cs`.

В обох класах:

- наслідування `BackgroundService`;
- `CreateChannelAsync`;
- `BasicQosAsync` з `PrefetchCount = 10`;
- `AsyncEventingBasicConsumer`;
- `BasicConsumeAsync(..., autoAck: false, ...)`;
- `StopAsync` скасовує consumer; непідтверджене повідомлення може бути доставлене повторно після закриття каналу.

Реєстрація: `AddHostedService<OrdersCatalogEventsConsumer>()` у `Services/Orders/Orders.Api/Program.cs` і `AddHostedService<ReviewsCatalogEventsConsumer>()` у `Services/Reviews/Reviews.WebApi/Program.cs`.

**Перевірка:** логи `... Consumer started. Queue: ...`; у коді підтвердження вимкнене через `autoAck: false`.

### 8. ACK після успіху, NACK без requeue для постійних збоїв — 0,8 бала

**Де:** метод `OnMessageAsync` в обох consumers.

- Успішна обробка: `BasicAckAsync(ea.DeliveryTag, multiple: false)`.
- Постійні помилки (наприклад, `JsonException`, `InvalidDataException`, невалідні аргументи, невідомий тип): `BasicNackAsync(..., requeue: false)`, після чого RabbitMQ використовує DLX.
- Тимчасові помилки передаються в `RetryOrDeadLetterAsync`.
- Під час shutdown повідомлення не підтверджується, щоб брокер міг повторно доставити його.

**Перевірка:** передати некоректний JSON або непідтримуваний тип події та переконатися, що повідомлення з'явилося у відповідній DLQ. Успішне повідомлення має зникнути з робочої черги після ACK.

### 9. Ідемпотентні обробники з таблицею/колекцією оброблених подій — 1,1 бала

**Orders, SQL Server:**

- `db/orders/schema.sql`: `dbo.ProcessedEvents` з `EventId UNIQUEIDENTIFIER PRIMARY KEY` і `ProcessedAt`.
- `Services/Orders/Orders.Dal/Repositories/ProcessedEventRepository.cs`, `TryAddAsync`: INSERT; SQL Server errors 2601/2627 означають дублікат.
- `Services/Orders/Orders.Dal/UnitOfWork.cs`: `ProcessedEvents`, `MenuItems` і транзакція використовують спільні connection/transaction.
- `Services/Orders/Orders.Bll/IntegrationEvents/Handlers/MenuItemPriceChangedHandler.cs` і `MenuItemRenamedHandler.cs`: `BeginTransactionAsync` → `TryAddAsync` → зміна локального довідника → `CommitAsync`; при помилці — Rollback.

**Reviews, MongoDB:**

- `Services/Reviews/Reviews.Infrastructure/Persistence/MongoDbContext.cs`: колекція `processed_events`.
- `Services/Reviews/Reviews.Infrastructure/IntegrationEvents/ProductRenamedHandler.cs`: EventId зберігається як `_id`; наявність події перевіряється перед оновленням; унікальність `_id` не дозволяє двічі зареєструвати той самий EventId; timestamp-фільтр захищає від повторного застосування тієї самої/старішої версії.

**Важлива відмінність:** у Reviews `UpdateManyAsync` і вставка в `processed_events` не виконуються в одній MongoDB-транзакції. Тому це ідемпотентна схема на рівні EventId + timestamp-фільтра, але не атомарна транзакція між двома колекціями.

**Перевірка:** повторно доставити подію з тим самим EventId. У Orders очікується лог `Duplicate ... event skipped`; у Reviews — `Duplicate event ... skipped` або повідомлення про вже зареєстрований EventId. Перевірити `dbo.ProcessedEvents` і `processed_events`.

### 10. Реплікація копій даних за політикою Лаб. 01 — 1,1 бала

**Orders:**

- `Services/Orders/Orders.Dal/Repositories/MenuItemRepository.cs`:
  - `ApplyPriceChangedAsync` оновлює `Name`, `Price`, `SourceUpdatedAt`;
  - `ApplyRenamedAsync` оновлює `Name`, `SourceUpdatedAt`;
  - обидва методи застосовують подію лише якщо `SourceUpdatedAt IS NULL OR SourceUpdatedAt < @OccurredAt`.
- `MenuItemPriceChangedHandler` передає нову ціну й назву.
- `MenuItemRenamedHandler` передає нову назву.

**Reviews:**

- `Services/Reviews/Reviews.Infrastructure/IntegrationEvents/ProductRenamedHandler.cs`: `UpdateManyAsync` оновлює `menuItemName` у відгуках відповідного `MenuItemId`, якщо `catalogNameUpdatedAtTicks` відсутній або менший за `OccurredAt.Ticks`.

**Що не змінюється:** `dbo.OrderItems.ProductName` і `dbo.OrderItems.UnitPrice` залишаються історичним знімком на момент оформлення замовлення. Reviews не зберігає локальну копію ціни та ігнорує `MenuItemPriceChangedEvent` після валідації.

**Перевірка SQL:**

```sql
SELECT Id, Name, Price, SourceUpdatedAt
FROM dbo.MenuItems
WHERE Id = <MenuItemId>;

SELECT oi.OrderId, oi.MenuItemId, oi.ProductName, oi.UnitPrice,
       mi.Name AS CurrentName, mi.Price AS CurrentCatalogPrice
FROM dbo.OrderItems AS oi
JOIN dbo.MenuItems AS mi ON mi.Id = oi.MenuItemId
WHERE oi.MenuItemId = <MenuItemId>;
```

У MongoDB Compass: `reviews` → фільтр `{ menuItemId: <MenuItemId> }`, поля `menuItemName`, `catalogNameUpdatedAtTicks`.

**Обсяг:** синхронізуються перейменування та зміна ціни. Створення/видалення товарів як integration events у поточному коді відсутні.

### 11. Повтори з лічильником x-retry-count і максимумом спроб — 0,9 бала

**Де:** `RetryOrDeadLetterAsync` у двох consumers.

- `MaxRetries = 3`;
- `TryParseRetryCount` підтримує значення `byte[]` і конвертовані рядкові/числові значення;
- кожна повторна публікація збільшує `x-retry-count`;
- повторне повідомлення публікується у відповідну робочу чергу через default exchange (`exchange: string.Empty`, `routingKey: QueueName`);
- оригінал ACK-иться після завершення `BasicPublishAsync`;
- після вичерпання трьох повторів оригінал NACK-иться з `requeue: false` і переходить у DLQ.

**Перевірка:** контрольовано викликати тимчасову помилку обробника та переглянути логи `retry {Attempt}/{MaxRetries}`. Після вичерпання ліміту перевірити відповідну DLQ та заголовок `x-retry-count`.

**Обмеження:** затримки/backoff і окремої retry-черги з TTL немає; повторна публікація не використовує Publisher Confirms.

### 12. Передача контексту трасування через брокер — 1,0 бала

**Де:**

- `Platform.EventBus/Diagnostics/EventBusDiagnostics.cs`: ActivitySource `Platform.Messaging`.
- `Platform.EventBus/Tracing/TracePropagation.cs`:
  - `Inject` використовує `Propagators.DefaultTextMapPropagator` для запису W3C trace context у headers;
  - `StartConsumerActivity` виконує `Extract`, створює `ActivityKind.Consumer` і додає messaging-теги.
- `Platform.EventBus/Publishing/RabbitMqEventPublisher.cs`: створює `ActivityKind.Producer`, додає теги повідомлення й викликає `TracePropagation.Inject`.
- `Services/Orders/Orders.Api/IntegrationEvents/OrdersCatalogEventsConsumer.cs` і `Services/Reviews/Reviews.WebApi/IntegrationEvents/ReviewsCatalogEventsConsumer.cs`: створюють consumer activity через `TracePropagation.StartConsumerActivity`.
- `Platform.ServiceDefaults/Extensions.cs`: `.AddSource(EventBusDiagnostics.SourceName)`.

**Перевірка:** змінити товар у Catalog, відкрити Aspire Dashboard → Traces. Перевірити producer span і consumer spans; зіставити TraceId у логах Publisher і Consumers. Перевірити CorrelationId окремо від TraceId.

### 13. Структуроване логування збоїв із бізнес-контекстом — 0,4 бала

**Де:**

- `Platform.ServiceDefaults/Extensions.cs`: Serilog, `CompactJsonFormatter`, `Enrich.FromLogContext`, властивості `ServiceName`, `Environment`, `MachineName`, `ThreadId`.
- `Platform.ServiceDefaults/CorrelationId.cs`: `CorrelationIdMiddleware` додає CorrelationId до `LogContext`, логів початку/завершення запиту та HTTP-відповіді.
- `Platform.EventBus/Publishing/RabbitMqEventPublisher.cs`: лог публікації з `EventType`, `EventId`, `TraceId`, `CorrelationId`, `Exchange`, `RoutingKey`.
- Orders/Reviews consumers: логи отримання, ACK, постійних помилок, Retry та DLQ з `MessageId`, `EventType`, `Queue`/`CorrelationId`.
- Orders handlers: `MenuItemId`, `OldPrice`, `NewPrice`, `OldName`, `NewName`, `EventId`, `CorrelationId`.
- `ProductRenamedHandler`: `MatchedReviews`, `ModifiedReviews`.

**Перевірка:** виконати успішне перейменування/зміну ціни та контрольований сценарій помилки. Переглянути JSON-логи в Aspire Dashboard або консолі; переконатися, що подія пов'язується за `EventId` і `CorrelationId`, а винятки передаються параметром `ex` у `LogError`/`LogWarning`.

## Підсумкова перевірка перед здачею

- [ ] `dotnet restore .\Platform.slnx` завершується успішно.
- [ ] `dotnet build .\Platform.slnx --nologo` завершується без помилок.
- [ ] Aspire Dashboard показує `messaging`, `catalog-api`, `orders-api`, `reviews-api`.
- [ ] RabbitMQ Management UI показує exchanges, робочі queues та DLQ.
- [ ] Зміна ціни в Catalog оновлює `Orders.dbo.MenuItems.Price`.
- [ ] Перейменування в Catalog оновлює Orders і `reviews.menuItemName`.
- [ ] `OrderItems.UnitPrice` та `OrderItems.ProductName` не переписуються.
- [ ] Повторна подія з тим самим EventId не змінює стан повторно.
- [ ] Некоректне повідомлення потрапляє в DLQ.
- [ ] Тимчасова помилка збільшує `x-retry-count`; після ліміту повідомлення потрапляє в DLQ.
- [ ] Aspire Traces показує Producer/Consumer spans з контекстом.
- [ ] Логи містять EventId, EventType, CorrelationId та бізнес-ідентифікатори.

## Примітки за результатами аналізу архіву

1. У наданому ZIP не знайдено окремих test projects.
2. У поточному коді немає Publisher Confirms і Outbox.
3. Retry не має затримки/backoff та окремої retry-черги.
4. Reviews не використовує MongoDB-транзакцію для спільного запису оновлених відгуків і `processed_events`.
5. Немає integration events для створення й видалення товару.
6. Збірку й практичні сценарії необхідно виконати локально: у середовищі підготовки документації .NET SDK недоступний.

