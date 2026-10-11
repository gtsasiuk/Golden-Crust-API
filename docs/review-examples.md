# Приклади документів MongoDB (golden_crust_reviews)

Повний набір документів: `db/reviews/seed.js`. Нижче скорочені приклади для кожного прийому.

## reviews: embedding + reference + денормалізація
```json
{
  "_id": "ObjectId('66f0c2000000000000000001')",
  "menuItemId": 1,                       // посилання на Catalog.MenuItems.Id (без FK)
  "menuItemName": "Маргарита",           // денормалізовано з Catalog (оновлюється подіями, лаба 8)
  "orderId": 1,                          // посилання на Orders.Orders.Id (перевірений відгук)
  "author": { "customerId": 1, "displayName": "Олена" },   // вбудований об'єкт (знімок з Orders)
  "type": "text",
  "rating": 5,
  "text": "Тісто тонке, соус насичений.",
  "comments": [                          // вбудований масив (небагато коментарів, завжди потрібні разом)
    { "author": { "customerId": 2, "displayName": "Андрій" }, "text": "Погоджуюсь", "createdAt": "ISODate('2026-10-02T09:15:00Z')" }
  ],
  "discussionId": "ObjectId('66f0c3000000000000000001')",  // reference на discussions
  "createdAt": "ISODate('2026-10-01T18:30:00Z')"
}
```

## reviews: гнучка схема (інший набір полів у тій самій колекції)
```json
{ "menuItemId": 1, "menuItemName": "Маргарита", "author": { "customerId": 2, "displayName": "Андрій" },
  "type": "photo", "rating": 4, "text": "Смачно", "photos": ["https://.../r2-1.jpg", "https://.../r2-2.jpg"], "createdAt": "..." }

{ "menuItemId": 1, "menuItemName": "Маргарита", "author": { "customerId": 3, "displayName": "Ірина" },
  "type": "quick", "rating": 5, "createdAt": "..." }

{ "menuItemId": 2, "menuItemName": "Пепероні", "orderId": 2, "author": { "customerId": 1, "displayName": "Олена" },
  "type": "text", "rating": 4, "text": "Добре", "pros": ["багато пепероні"], "cons": ["жирна"], "createdAt": "..." }
```

## discussions + discussionMessages: reference для необмеженої підколекції
```json
// discussions: лише заголовок
{ "_id": "ObjectId('66f0c3000000000000000001')", "menuItemId": 1, "reviewId": "ObjectId('66f0c2000000000000000001')",
  "title": "Маргарита без базиліку?", "status": "open",
  "createdBy": { "customerId": 3, "displayName": "Ірина" }, "createdAt": "...", "messageCount": 3, "lastMessageAt": "..." }

// discussionMessages: кожне повідомлення окремим документом
{ "discussionId": "ObjectId('66f0c3000000000000000001')", "author": { "customerId": 3, "displayName": "Ірина" },
  "text": "Чи можна без базиліку?", "createdAt": "..." }
```

## ratings: гібридний підхід (aggregate document + subset pattern)
```json
{
  "menuItemId": 1, "menuItemName": "Маргарита",
  "average": 4.67, "count": 3,
  "distribution": { "1": 0, "2": 0, "3": 0, "4": 1, "5": 2 },        // вбудовано
  "latest": [                                                          // вбудовано 3 останні (subset)
    { "reviewId": "ObjectId('66f0c2000000000000000003')", "rating": 5, "createdAt": "..." }   // reference на reviews
  ],
  "updatedAt": "..."
}
```

## JSON Schema validation (скорочено, повна версія: db/reviews/collections.js)
```javascript
db.createCollection("reviews", { validator: { $jsonSchema: {
  bsonType: "object",
  required: ["menuItemId", "menuItemName", "author", "type", "rating", "createdAt"],
  properties: { rating: { bsonType: "number", minimum: 1, maximum: 5, multipleOf: 1 } }
} } });
```
