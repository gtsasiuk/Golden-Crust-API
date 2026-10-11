// =====================================================================
// Golden Crust | контекст REVIEWS | MongoDB
// Створює колекції з JSON Schema validation та індекси. Ідемпотентний:
// існуючі колекції оновлюються через collMod, індекси - через createIndex.
// Запуск: mongosh "mongodb://localhost:27017" --file db/reviews/collections.js
// =====================================================================
const database = db.getSiblingDB("golden_crust_reviews");

function ensureCollection(name, validator) {
  const options = { validator, validationLevel: "strict", validationAction: "error" };
  if (database.getCollectionNames().includes(name)) {
    database.runCommand({ collMod: name, ...options });
  } else {
    database.createCollection(name, options);
  }
}

// Схема навмисно ГНУЧКА: required-поля мінімальні, additionalProperties не забороняємо,
// тож документи в одній колекції можуть мати різні набори полів
// (photos, pros/cons, comments, orderId, discussionId, text ...).
const authorSchema = {
  bsonType: "object",
  required: ["customerId", "displayName"],
  properties: {
    customerId:  { bsonType: "number", description: "Id клієнта з БД Orders (посилання без FK)" },
    displayName: { bsonType: "string", minLength: 1 }
  }
};

// 1) reviews: embedding (author, comments), reference (discussionId, orderId),
//    денормалізація (menuItemName), різні типи відгуків (type).
ensureCollection("reviews", {
  $jsonSchema: {
    bsonType: "object",
    required: ["menuItemId", "menuItemName", "author", "type", "rating", "createdAt"],
    properties: {
      menuItemId:   { bsonType: "number", minimum: 1, description: "посилання на Catalog.MenuItems.Id (без FK)" },
      menuItemName: { bsonType: "string", minLength: 1, description: "денормалізована копія з Catalog" },
      orderId:      { bsonType: "number", description: "посилання на Orders.Orders.Id (перевірений відгук)" },
      author:       authorSchema,
      type:         { enum: ["quick", "text", "photo"] },
      rating:       { bsonType: "number", minimum: 1, maximum: 5, multipleOf: 1 },
      text:         { bsonType: "string", maxLength: 2000 },
      photos:       { bsonType: "array", maxItems: 5, items: { bsonType: "string" } },
      pros:         { bsonType: "array", items: { bsonType: "string" } },
      cons:         { bsonType: "array", items: { bsonType: "string" } },
      comments: {
        bsonType: "array",
        items: {
          bsonType: "object",
          required: ["author", "text", "createdAt"],
          properties: {
            author:    authorSchema,
            text:      { bsonType: "string", minLength: 1, maxLength: 1000 },
            createdAt: { bsonType: "date" }
          }
        }
      },
      discussionId: { bsonType: "objectId", description: "reference на колекцію discussions" },
      createdAt:    { bsonType: "date" }
    }
  }
});

// 2) discussions: заголовок обговорення (reference на відгук / страву).
ensureCollection("discussions", {
  $jsonSchema: {
    bsonType: "object",
    required: ["menuItemId", "title", "status", "createdBy", "createdAt", "messageCount"],
    properties: {
      menuItemId:    { bsonType: "number", minimum: 1 },
      reviewId:      { bsonType: "objectId" },
      title:         { bsonType: "string", minLength: 1, maxLength: 200 },
      status:        { enum: ["open", "closed"] },
      createdBy:     authorSchema,
      createdAt:     { bsonType: "date" },
      messageCount:  { bsonType: "number", minimum: 0 },
      lastMessageAt: { bsonType: "date" }
    }
  }
});

// 3) discussionMessages: повідомлення ОКРЕМИМИ документами (reference на discussions),
//    бо обговорення може мати тисячі повідомлень: вбудовувати їх небезпечно.
ensureCollection("discussionMessages", {
  $jsonSchema: {
    bsonType: "object",
    required: ["discussionId", "author", "text", "createdAt"],
    properties: {
      discussionId: { bsonType: "objectId" },
      author:       authorSchema,
      text:         { bsonType: "string", minLength: 1, maxLength: 2000 },
      replyTo:      { bsonType: "objectId" },
      createdAt:    { bsonType: "date" }
    }
  }
});

// 4) ratings: ГІБРИД. Агрегат по страві: вбудовані distribution і latest (subset pattern:
//    3 останні відгуки), а повний список лишається в reviews (reference через reviewId).
ensureCollection("ratings", {
  $jsonSchema: {
    bsonType: "object",
    required: ["menuItemId", "menuItemName", "average", "count", "distribution", "updatedAt"],
    properties: {
      menuItemId:   { bsonType: "number", minimum: 1 },
      menuItemName: { bsonType: "string", minLength: 1, description: "денормалізована копія з Catalog" },
      average:      { bsonType: "number", minimum: 0, maximum: 5 },
      count:        { bsonType: "number", minimum: 0 },
      distribution: {
        bsonType: "object",
        required: ["1", "2", "3", "4", "5"],
        properties: {
          "1": { bsonType: "number", minimum: 0 }, "2": { bsonType: "number", minimum: 0 },
          "3": { bsonType: "number", minimum: 0 }, "4": { bsonType: "number", minimum: 0 },
          "5": { bsonType: "number", minimum: 0 }
        }
      },
      latest: {
        bsonType: "array",
        maxItems: 3,
        items: {
          bsonType: "object",
          required: ["reviewId", "rating", "createdAt"],
          properties: {
            reviewId:  { bsonType: "objectId" },
            rating:    { bsonType: "number", minimum: 1, maximum: 5 },
            snippet:   { bsonType: "string", maxLength: 140 },
            createdAt: { bsonType: "date" }
          }
        }
      },
      updatedAt: { bsonType: "date" }
    }
  }
});

// ---- Індекси (кожен під конкретний запит) --------------------------------
// "відгуки на страву, нові першими" (картка страви)
database.reviews.createIndex({ menuItemId: 1, createdAt: -1 }, { name: "ix_reviews_menuItem_createdAt" });
// "мої відгуки" (профіль)
database.reviews.createIndex({ "author.customerId": 1, createdAt: -1 }, { name: "ix_reviews_author_createdAt" });
// один відгук на страву в межах одного замовлення (лише там, де orderId є)
database.reviews.createIndex({ orderId: 1, menuItemId: 1 },
  { name: "ux_reviews_order_menuItem", unique: true, partialFilterExpression: { orderId: { $exists: true } } });
// "обговорення страви, найактивніші першими"
database.discussions.createIndex({ menuItemId: 1, lastMessageAt: -1 }, { name: "ix_discussions_menuItem_lastMessage" });
// "повідомлення обговорення за часом" (пагінація чату)
database.discussionMessages.createIndex({ discussionId: 1, createdAt: 1 }, { name: "ix_messages_discussion_createdAt" });
// один агрегований рейтинг на страву + швидкий доступ за menuItemId
database.ratings.createIndex({ menuItemId: 1 }, { name: "ux_ratings_menuItem", unique: true });

print("Collections: " + database.getCollectionNames().sort().join(", "));
