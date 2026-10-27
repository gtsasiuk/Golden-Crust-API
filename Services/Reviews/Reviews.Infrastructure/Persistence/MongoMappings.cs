using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using Reviews.Domain.Common;
using Reviews.Domain.Entities;
using Reviews.Domain.Enums;
using Reviews.Domain.ValueObjects;

namespace Reviews.Infrastructure.Persistence;

public static class MongoMappings
{
    private static readonly object Lock = new();
    private static bool _registered;

    public static void Register()
    {
        lock (Lock)
        {
            if (_registered) return;

            ConventionRegistry.Register("ReviewsConventions",
                new ConventionPack
                {
                    new CamelCaseElementNameConvention(),
                    new IgnoreExtraElementsConvention(true),
                    new IgnoreIfNullConvention(true)
                },
                t => t.Namespace is not null && t.Namespace.StartsWith("Reviews."));

            BsonClassMap.RegisterClassMap<BaseEntity>(cm =>
            {
                cm.MapIdProperty(e => e.Id);
                cm.MapProperty(e => e.CreatedAt).SetElementName("createdAt");
                cm.MapProperty(e => e.UpdatedAt).SetElementName("updatedAt").SetIgnoreIfNull(true);
                cm.MapProperty(e => e.Version).SetElementName("version");
            });

            BsonClassMap.RegisterClassMap<Author>(cm =>
            {
                cm.MapProperty(a => a.CustomerId).SetElementName("customerId");
                cm.MapProperty(a => a.DisplayName).SetElementName("displayName");
                cm.MapCreator(a => new Author(a.CustomerId, a.DisplayName));
                cm.SetIgnoreExtraElements(true);
            });

            BsonClassMap.RegisterClassMap<Comment>(cm =>
            {
                cm.MapProperty(c => c.Author).SetElementName("author");
                cm.MapProperty(c => c.Text).SetElementName("text");
                cm.MapProperty(c => c.CreatedAt).SetElementName("createdAt");
                cm.MapCreator(c => new Comment(c.Author, c.Text, c.CreatedAt));
                cm.SetIgnoreExtraElements(true);
            });

            BsonClassMap.RegisterClassMap<Review>(cm =>
            {
                cm.MapProperty(r => r.MenuItemId).SetElementName("menuItemId");
                cm.MapProperty(r => r.MenuItemName).SetElementName("menuItemName");
                cm.MapProperty(r => r.OrderId).SetElementName("orderId").SetIgnoreIfNull(true);
                cm.MapProperty(r => r.Author).SetElementName("author");
                cm.MapProperty(r => r.Type).SetElementName("type")
                    .SetSerializer(new LowerCaseEnumSerializer<ReviewType>());
                cm.MapProperty(r => r.Rating).SetElementName("rating");
                cm.MapProperty(r => r.Text).SetElementName("text").SetIgnoreIfNull(true);
                cm.MapProperty(r => r.DiscussionId).SetElementName("discussionId").SetIgnoreIfNull(true);

                cm.MapField("_photos").SetElementName("photos")
                    .SetShouldSerializeMethod(o => ((Review)o).Photos.Count > 0);
                cm.MapField("_pros").SetElementName("pros")
                    .SetShouldSerializeMethod(o => ((Review)o).Pros.Count > 0);
                cm.MapField("_cons").SetElementName("cons")
                    .SetShouldSerializeMethod(o => ((Review)o).Cons.Count > 0);
                cm.MapField("_comments").SetElementName("comments")
                    .SetShouldSerializeMethod(o => ((Review)o).Comments.Count > 0);

                cm.SetIgnoreExtraElements(true);
            });

            BsonClassMap.RegisterClassMap<Discussion>(cm =>
            {
                cm.MapProperty(d => d.MenuItemId).SetElementName("menuItemId");
                cm.MapProperty(d => d.ReviewId).SetElementName("reviewId").SetIgnoreIfNull(true);
                cm.MapProperty(d => d.Title).SetElementName("title");
                cm.MapProperty(d => d.Status).SetElementName("status")
                    .SetSerializer(new LowerCaseEnumSerializer<DiscussionStatus>());
                cm.MapProperty(d => d.CreatedBy).SetElementName("createdBy");
                cm.MapProperty(d => d.MessageCount).SetElementName("messageCount");
                cm.MapProperty(d => d.LastMessageAt).SetElementName("lastMessageAt").SetIgnoreIfNull(true);
                cm.SetIgnoreExtraElements(true);
            });

            _registered = true;
        }
    }
}