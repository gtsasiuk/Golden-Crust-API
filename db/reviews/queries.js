// =====================================================================
// Golden Crust | REVIEWS | приклади запитів, агрегацій і перевірок схеми
// Запуск: mongosh "mongodb://localhost:27017" --file db/reviews/queries.js
// =====================================================================
const database = db.getSiblingDB("golden_crust_reviews");

print("\n== 1. Відгуки на Маргариту, нові першими (ix_reviews_menuItem_createdAt)");
printjson(database.reviews.find({ menuItemId: 1 }, { type: 1, rating: 1, createdAt: 1 }).sort({ createdAt: -1 }).toArray());

print("\n== 2. ГНУЧКА СХЕМА: різні набори полів в одній колекції");
printjson(database.reviews.find({}, { type: 1, text: 1, photos: 1, pros: 1, comments: 1, orderId: 1 }).toArray());

print("\n== 3. Лише перевірені відгуки (є orderId) і лише з фото");
printjson(database.reviews.find({ orderId: { $exists: true } }, { menuItemName: 1, orderId: 1 }).toArray());
printjson(database.reviews.find({ photos: { $exists: true } }, { menuItemName: 1, photos: 1 }).toArray());

print("\n== 4. Агрегація: перерахунок середньої оцінки й розподілу з reviews (звіряється з ratings)");
printjson(database.reviews.aggregate([
  { $group: { _id: "$menuItemId", menuItemName: { $first: "$menuItemName" },
              count: { $sum: 1 }, average: { $avg: "$rating" } } },
  { $project: { menuItemName: 1, count: 1, average: { $round: ["$average", 2] } } },
  { $sort: { _id: 1 } }
]).toArray());

print("\n== 5. Агрегація: кількість вбудованих коментарів на відгук ($unwind / $size)");
printjson(database.reviews.aggregate([
  { $match: { comments: { $exists: true } } },
  { $project: { menuItemName: 1, commentsCount: { $size: "$comments" } } }
]).toArray());

print("\n== 6. Reference: відгук + його обговорення ($lookup)");
printjson(database.reviews.aggregate([
  { $match: { discussionId: { $exists: true } } },
  { $lookup: { from: "discussions", localField: "discussionId", foreignField: "_id", as: "discussion" } },
  { $project: { menuItemName: 1, "discussion.title": 1, "discussion.messageCount": 1 } }
]).toArray());

print("\n== 7. Повідомлення обговорення з пагінацією (ix_messages_discussion_createdAt)");
const discussion = database.discussions.findOne({}, { _id: 1 });
printjson(database.discussionMessages.find({ discussionId: discussion._id }).sort({ createdAt: 1 }).skip(0).limit(2).toArray());

print("\n== 8. Гібрид: агрегований рейтинг страви з 3 останніми відгуками одним читанням");
printjson(database.ratings.findOne({ menuItemId: 1 }, { menuItemName: 1, average: 1, count: 1, distribution: 1, latest: 1 }));

print("\n== 9. Схема валідується: ці вставки МАЮТЬ завершитись помилкою");
const bad = [
  { label: "rating = 7",       doc: { menuItemId: 1, menuItemName: "Маргарита", author: { customerId: 1, displayName: "Тест" }, type: "text", rating: 7, createdAt: new Date() } },
  { label: "немає author",     doc: { menuItemId: 1, menuItemName: "Маргарита", type: "quick", rating: 5, createdAt: new Date() } },
  { label: "type = 'video'",   doc: { menuItemId: 1, menuItemName: "Маргарита", author: { customerId: 1, displayName: "Тест" }, type: "video", rating: 5, createdAt: new Date() } }
];
bad.forEach(function (c) {
  try {
    database.reviews.insertOne(c.doc);
    print("UNEXPECTED: вставка '" + c.label + "' пройшла");
    database.reviews.deleteOne({ _id: c.doc._id });
  } catch (e) {
    print("OK  (" + c.label + ") -> " + e.codeName);
  }
});

print("\n== 10. Індекси колекції reviews");
printjson(database.reviews.getIndexes().map(function (i) { return i.name; }));
