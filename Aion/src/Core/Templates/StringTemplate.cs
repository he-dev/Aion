using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aion.Util.Scriban;

namespace Aion.Core.Templates;

// core: Use this type for all string-templates, so you don't forget to render them.
public class StringTemplate(string template)
{
    public string Render(IImmutableList<VariableGroup> variables) => RendersTemplates.In(template, variables);
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

// todo: No idea how to name this class.
public class StringTemplate2(string template)
{
    private string? Value { get; set; }

    public void Render(IImmutableList<VariableGroup> variables) => Value = RendersTemplates.In(template, variables);

    public override string ToString() => Value ?? throw new InvalidOperationException("Template has not been rendered yet.");

    public static implicit operator string(StringTemplate2 template) => template.ToString();
}

public class StringTemplate2Converter : JsonConverter<StringTemplate2>
{
    public override StringTemplate2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new StringTemplate2(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, StringTemplate2 value, JsonSerializerOptions options)
    {
        throw new NotImplementedException("This method is not supported.");
    }
}