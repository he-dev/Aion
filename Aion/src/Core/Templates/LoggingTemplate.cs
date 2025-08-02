using System;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Util.Json;
using Aion.Util.Scriban;

namespace Aion.Core.Templates;

// core: Use this type for all logging-templates, so you don't forget to render them.
public class LoggingTemplate(JsonObject template)
{
    public async Task<JsonObject> RenderAsync(Profile profile, IImmutableList<VariableGroup> variables)
    {
        // core: Use the logger configuration that is embedded in the workflow.
        if (template.ContainsKey("WriteTo"))
        {
            return RenderFilePaths(template, variables);
        }

        if (await TryGetLoggingPreset(template, profile, variables) is { } preset)
        {
            return RenderFilePaths(preset, variables);
        }

        // core: Something else has been specified.
        throw new InvalidLoggingConfigurationException();
    }

    private static async Task<JsonObject?> TryGetLoggingPreset(JsonObject logging, Profile profile, IImmutableList<VariableGroup> variables)
    {
        // core: Use the logger configuration that is specified by the preset.
        if (!logging.ContainsKey(nameof(LoggingPreset.Info.File))) return null;
        if (!logging.ContainsKey(nameof(LoggingPreset.Info.Name))) return null;

        var presetInfo = logging.Deserialize<LoggingPreset.Info>(new JsonSerializerOptions
        {
            Converters = { new StringTemplateConverter() }
        })!;

        var file = presetInfo.File.Render(variables);
        var name = presetInfo.Name.Render(variables);

        return await profile.LoggingPreset(file, name);
    }

    // core: Renders each path property it finds that looks like a template.
    private static JsonObject RenderFilePaths(JsonObject serilog, IImmutableList<VariableGroup> variables)
    {
        // meta: It needs to be cloned otherwise the original object will be modified, and that's a bad thing.
        serilog = serilog.Clone();

        // core: Scan sinks for the "path" property and run it through the template engine.
        if (serilog["WriteTo"] is JsonArray writeTo)
        {
            foreach (var sink in writeTo)
            {
                // meta: Make sure the path property really exists.
                if (sink is not null && sink["Name"]?.GetValue<string>() == "File" && sink["Args"] is JsonObject args && args["path"] is JsonValue path)
                {
                    var template = path.GetValue<string>();
                    args["path"] = RendersTemplates.In(template, variables);
                }
            }
        }

        return serilog;
    }
}

public class LoggingTemplateConverter : JsonConverter<LoggingTemplate>
{
    public override LoggingTemplate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var template = JsonNode.Parse(ref reader)!.AsObject();
        return new LoggingTemplate(template);
    }

    public override void Write(Utf8JsonWriter writer, LoggingTemplate value, JsonSerializerOptions options)
    {
        throw new NotImplementedException("This method is not supported.");
    }
}

public class InvalidLoggingConfigurationException()
    : Exception("Unknown logging configuration. Expected either 'WriteTo' property or logging preset reference.");