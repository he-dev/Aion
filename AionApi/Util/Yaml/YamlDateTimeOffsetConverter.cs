using System;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace AionApi.Util.Yaml;

public class YamlDateTimeOffsetConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(DateTimeOffset);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var scalar = parser.Consume<Scalar>();
        return DateTimeOffset.Parse(scalar.Value, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value is DateTimeOffset dto)
        {
            emitter.Emit(new Scalar(dto.ToString("o"))); // ISO 8601 format
        }
    }
}