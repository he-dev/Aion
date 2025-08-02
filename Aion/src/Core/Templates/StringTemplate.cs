using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aion.Util.Scriban;

namespace Aion.Core.Templates;

// core: Use this type for all string-templates, so you don't forget to render them.
public class StringTemplate(string value)
{
    public string Render(IImmutableList<VariableGroup> variables) => RendersTemplates.In(value, variables);
}

public static class StringTemplateExtensions
{
    public static IEnumerable<string> Render(this IEnumerable<StringTemplate> templates, IImmutableList<VariableGroup> variables)
    {
        return templates.Select(t => t.Render(variables));
    }
}

public class StringTemplateConverter : JsonConverter<StringTemplate>
{
    public override StringTemplate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new StringTemplate(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, StringTemplate value, JsonSerializerOptions options)
    {
        throw new NotImplementedException("This method is not supported.");
    }
}
