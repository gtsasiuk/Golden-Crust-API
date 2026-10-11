// =====================================================================
// Golden Crust | REVIEWS | тестові дані
// Ідемпотентний: фіксовані _id + replaceOne({upsert: true}) = find-or-create.
// Запуск: mongosh "mongodb://localhost:27017" --file db/reviews/seed.js
// menuItemId / customerId / orderId відповідають seed-даним Catalog та Orders.
// =====================================================================
const database = db.getSiblingDB("golden_crust_reviews");

function upsert(collection, doc) {
  database.getCollection(collection).replaceOne({ _id: doc._id }, doc, { upsert: true });
}

const R1 = ObjectId("66f0c2000000000000000001");
const R2 = ObjectId("66f0c2000000000000000002");
const R3 = ObjectId("66f0c2000000000000000003");
const R4 = ObjectId("66f0c2000000000000000004");
const R5 = ObjectId("66f0c2000000000000000005");
const D1 = ObjectId("66f0c3000000000000000001");

const olena  = { customerId: 1, displayName: "Олена" };
const andriy = { customerId: 2, displayName: "Андрій" };
const iryna  = { customerId: 3, displayName: "Ірина" };

// ---- reviews: документи з РІЗНИМИ наборами полів ---------------------------
// R1: text + вбудовані comments + reference на discussion + перевірений orderId
upsert("reviews", {
  _id: R1, menuItemId: 1, menuItemName: "Маргарита", orderId: 1, author: olena,
  type: "text", rating: 5, text: "Тісто тонке, соус насичений. Приїхала ще гарячою.",
  comments: [
    { author: andriy, text: "Погоджуюсь, найкраща в місті", createdAt: ISODate("2026-10-02T09:15:00Z") },
    { author: iryna,  text: "А без базиліку можна замовити?", createdAt: ISODate("2026-10-02T11:40:00Z") }
  ],
  discussionId: D1, createdAt: ISODate("2026-10-01T18:30:00Z")
});

// R2: photo: єдиний документ із масивом photos
upsert("reviews", {
  _id: R2, menuItemId: 1, menuItemName: "Маргарита", author: andriy,
  type: "photo", rating: 4, text: "Смачно, але хотілося б більше сиру.",
  photos: ["https://cdn.goldencrust.example/reviews/r2-1.jpg", "https://cdn.goldencrust.example/reviews/r2-2.jpg"],
  createdAt: ISODate("2026-10-03T19:05:00Z")
});

// R3: quick: лише оцінка, без тексту (мінімальний набір полів)
upsert("reviews", {
  _id: R3, menuItemId: 1, menuItemName: "Маргарита", author: iryna,
  type: "quick", rating: 5, createdAt: ISODate("2026-10-04T12:00:00Z")
});

// R4: text + масиви pros/cons + orderId
upsert("reviews", {
  _id: R4, menuItemId: 2, menuItemName: "Пепероні", orderId: 2, author: olena,
  type: "text", rating: 4, text: "Добре, але трохи жирна.",
  pros: ["багато пепероні", "хрустке тісто"], cons: ["жирна"],
  createdAt: ISODate("2026-10-05T20:10:00Z")
});

// R5: text без додаткових полів
upsert("reviews", {
  _id: R5, menuItemId: 2, menuItemName: "Пепероні", author: andriy,
  type: "text", rating: 3, text: "Нормально, але очікував гостріше.",
  createdAt: ISODate("2026-10-06T14:20:00Z")
});

// ---- discussions (header) + discussionMessages (reference) ------------------
upsert("discussions", {
  _id: D1, menuItemId: 1, reviewId: R1, title: "Маргарита без базиліку?", status: "open",
  createdBy: iryna, createdAt: ISODate("2026-10-02T11:40:00Z"),
  messageCount: 3, lastMessageAt: ISODate("2026-10-02T13:05:00Z")
});

upsert("discussionMessages", {
  _id: ObjectId("66f0c4000000000000000001"), discussionId: D1, author: iryna,
  text: "Чи можна замовити Маргариту без базиліку?", createdAt: ISODate("2026-10-02T11:40:00Z")
});
upsert("discussionMessages", {
  _id: ObjectId("66f0c4000000000000000002"), discussionId: D1, author: olena,
  text: "Так, я писала про це в коментарі до замовлення, зробили.",
  replyTo: ObjectId("66f0c4000000000000000001"), createdAt: ISODate("2026-10-02T12:10:00Z")
});
upsert("discussionMessages", {
  _id: ObjectId("66f0c4000000000000000003"), discussionId: D1, author: iryna,
  text: "Дякую, спробую!", replyTo: ObjectId("66f0c4000000000000000002"),
  createdAt: ISODate("2026-10-02T13:05:00Z")
});

// ---- ratings: гібрид (distribution + latest вбудовані, reviewId як reference) ---
// Маргарита: оцінки 5, 4, 5 -> середня 4.67
upsert("ratings", {
  _id: ObjectId("66f0c5000000000000000001"), menuItemId: 1, menuItemName: "Маргарита",
  average: 4.67, count: 3, distribution: { "1": 0, "2": 0, "3": 0, "4": 1, "5": 2 },
  latest: [
    { reviewId: R3, rating: 5, createdAt: ISODate("2026-10-04T12:00:00Z") },
    { reviewId: R2, rating: 4, snippet: "Смачно, але хотілося б більше сиру.", createdAt: ISODate("2026-10-03T19:05:00Z") },
    { reviewId: R1, rating: 5, snippet: "Тісто тонке, соус насичений.", createdAt: ISODate("2026-10-01T18:30:00Z") }
  ],
  updatedAt: ISODate("2026-10-06T14:30:00Z")
});
// Пепероні: оцінки 4, 3 -> середня 3.5
upsert("ratings", {
  _id: ObjectId("66f0c5000000000000000002"), menuItemId: 2, menuItemName: "Пепероні",
  average: 3.5, count: 2, distribution: { "1": 0, "2": 0, "3": 1, "4": 1, "5": 0 },
  latest: [
    { reviewId: R5, rating: 3, snippet: "Нормально, але очікував гостріше.", createdAt: ISODate("2026-10-06T14:20:00Z") },
    { reviewId: R4, rating: 4, snippet: "Добре, але трохи жирна.", createdAt: ISODate("2026-10-05T20:10:00Z") }
  ],
  updatedAt: ISODate("2026-10-06T14:30:00Z")
});

print("reviews=" + database.reviews.countDocuments() +
      ", discussions=" + database.discussions.countDocuments() +
      ", messages=" + database.discussionMessages.countDocuments() +
      ", ratings=" + database.ratings.countDocuments());   // 5 / 1 / 3 / 2
