using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Reviews.Infrastructure.Persistence;

public sealed class LowerCaseEnumSerializer<TEnum> : SerializerBase<TEnum> where TEnum : struct, Enum
{
    public override TEnum Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        => Enum.Parse<TEnum>(context.Reader.ReadString(), ignoreCase: true);

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TEnum value)
        => context.Writer.WriteString(value.ToString().ToLowerInvariant());
}