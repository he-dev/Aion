using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Aion.Util.Scriban;

namespace Aion.Core.Templates;

// core: Use this type for all logging-templates, so you don't forget to render them.
public class ArgumentsTemplate(IList<string>? argList, string? argString)
{
    public Collection<string> RenderArgList(IImmutableList<VariableGroup> variables)
    {
        return new Collection<string>(
            argList is not null ? argList.Select(a => RendersTemplates.In(a, variables)).ToList() : []
        );
    }

    public string RenderArgString(IImmutableList<VariableGroup> variables)
    {
        return argString is not null ? RendersTemplates.In(argString, variables) : string.Empty;
    }
}

public class ArgumentsTemplateConverter : JsonConverter<ArgumentsTemplate>
{
    public override ArgumentsTemplate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonNode.Parse(ref reader) switch
        {
            JsonArray jsonArray => new ArgumentsTemplate(argList: jsonArray.GetValues<string>().ToList(), null),
            JsonValue jsonValue => new ArgumentsTemplate(null, argString: jsonValue.GetValue<string>()),
            _ => throw new JsonException("Expected an object or array.")
        };
    }

    public override void Write(Utf8JsonWriter writer, ArgumentsTemplate value, JsonSerializerOptions options)
    {
        throw new NotImplementedException("This method is not supported.");
    }
}