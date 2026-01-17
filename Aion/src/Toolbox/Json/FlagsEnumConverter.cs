using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aion.Toolbox.Json;

public class FlagsEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
        {
            return default;
        }

        var parts = value.Split('|', ',').Select(p => p.Trim());
        var result = 0;

        foreach (var part in parts)
        {
            if (Enum.TryParse<T>(part, ignoreCase: true, out var parsed))
            {
                result |= Convert.ToInt32(parsed);
            }
            else
            {
                throw new JsonException($"Unknown enum value: '{part}'");
            }
        }

        return (T)(object)result;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}