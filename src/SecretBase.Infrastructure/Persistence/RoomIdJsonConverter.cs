using System.Text.Json;
using System.Text.Json.Serialization;
using SecretBase.Core.Desktop;

namespace SecretBase.Infrastructure.Persistence;

public sealed class RoomIdJsonConverter : JsonConverter<RoomId>
{
    public override RoomId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return string.IsNullOrWhiteSpace(value) ? RoomId.DefaultRoomId : new RoomId(value);
    }

    public override void Write(Utf8JsonWriter writer, RoomId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
