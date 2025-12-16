using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aion.Core.Logging;

namespace Aion.Core.Workflows;

public record WorkflowTemplate
{
    // core: Make the user specify this value explicitly, so they don't activate workflows by accident.
    public bool Enabled { get; init; }

    [Cron]
    public string Cron { get; init; } = null!;

    public Dictionary<string, string> Variables { get; init; } = new();

    public Dictionary<string, string> Environment { get; init; } = new();

    //[SerilogOrPreset]
    public LoggingInfo Logging { get; init; } = new();

    public StepTemplate[] Steps { get; init; } = null!;

    public record StepTemplate
    {
        public string? Name { get; init; }

        public bool Enabled { get; init; } = true;

        [NotNullOrWhiteSpace]
        public string FileName { get; init; } = null!;

        [JsonConverter(typeof(CommandLineArgumentConverter))]
        public IImmutableList<CommandLineArgument> Arguments { get; init; } = [];

        public Dictionary<string, string> Environment { get; init; } = new();

        public string? WorkingDirectory { get; init; }

        public TimeSpan? Timeout { get; init; }

        //[SerilogOrPreset]
        public LoggingInfo Logging { get; init; } = new();

        public string? DependsOn { get; init; }
    }

    public static async Task<WorkflowTemplate> FromFile(string path)
    {
        if (!File.Exists(path))
        {
            // note: This is pretty unlikely, but who knows...
            throw new FileNotFoundException($"Workflow template '{path}' not found.", fileName: path);
        }

        await using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var template = await JsonSerializer.DeserializeAsync<WorkflowTemplate>(fileStream, new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
        }) ?? throw new InvalidWorkflowTemplate(path);

        return WorkflowTemplateValidation.Validate(template);
    }
}

public class InvalidWorkflowTemplate(string path) : Exception($"File '{path}' is not a valid workflow template.");

public enum LoggingSource
{
    None,
    Auto,
    Preset,
    Custom,
}

public record LoggingInfo
{
    public LoggingSource Source { get; init; } = LoggingSource.Auto;
    public string? Preset { get; init; }
    public JsonObject? Custom { get; init; }

    public async Task<JsonObject?> Get(LoggingPresetRepository loggingPresets)
    {
        return Source switch
        {
            LoggingSource.Auto => Custom ?? await loggingPresets.Find(Preset),
            LoggingSource.Preset when Preset is not null => await loggingPresets.Find(Preset),
            LoggingSource.Custom when Custom is not null => Custom,
            _ => null
        };
    }
}

public class CommandLineArgumentConverter : JsonConverter<IImmutableList<CommandLineArgument>>
{
    public override IImmutableList<CommandLineArgument> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException($"Expected object but found {reader.TokenType}.");

        using var jsonDocument = JsonDocument.ParseValue(ref reader);

        return
            jsonDocument
                .RootElement
                .EnumerateStringOrArrayObject()
                .ToImmutableList();
    }

    public override void Write(Utf8JsonWriter writer, IImmutableList<CommandLineArgument> value, JsonSerializerOptions options)
    {
        throw new NotSupportedException();
    }
}

public static class JsonElementExtensions
{
    public static IEnumerable<CommandLineArgument> EnumerateStringOrArrayObject(this JsonElement element)
    {
        return
            from property in element.EnumerateObject()
            let values = property.Value.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.Array => property.Value.EnumerateArray().SelectPrimitives().ToArray(),
                JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => [property.Value.ToString()],
                _ => throw new JsonException($"Expected primitive or array for key '{property.Name}' but found '{property.Value.ValueKind}'.")
            }
            select new CommandLineArgument(property.Name, values);
    }

    private static IEnumerable<string> SelectPrimitives(this IEnumerable<JsonElement> elements)
    {
        foreach (var element in elements)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False:
                    yield return element.ToString();
                    break;
                case JsonValueKind.Null or JsonValueKind.Undefined:
                    // core: Ignore these values.
                    break;
                default:
                    // core: Anything else is invalid.
                    throw new JsonException($"Array elements must be primitives, but found '{element.ValueKind}'.");
            }
        }
    }
}

public static class ProcessStartInfoExtensions
{
    public static string RenderArguments(this IEnumerable<CommandLineArgument> arguments)
    {
        return string.Join(" ", arguments.Select(cla => cla.ToString()));
    }
}

public record CommandLineArgument(string Name, string[]? Values)
{
    private static readonly Regex SuffixPattern = new(@"(?<NameValueSeparator>[:=])?(?<ItemList>\[(?<ItemSeparator>[,;])?\])?$", RegexOptions.Compiled);

    private const string DefaultNameValueSeparator = " ";
    private const string DefaultItemSeparator = " ";

    public override string ToString()
    {
        // Raw passthrough: $ => single raw string, no processing
        if (Name == "$")
        {
            return
                Values is { Length: 1 }
                    ? Values[0]
                    : throw new InvalidOperationException("Raw passthrough '$' requires exactly one value.");
        }

        // Positional: _ => just values
        if (Name == "_")
        {
            return string.Join(" ", Values ?? []);
        }

        // Flag: --verbose => just key
        if (Values is null or { Length: 0 })
        {
            return
                // core: Make sure there is no list suffix, which could indicate missing values.
                SuffixPattern.Match(Name) is { Length: > 0 } suffixMatch && suffixMatch.Groups["ItemList"].Success
                    ? throw new InvalidOperationException($"Argument '{Name}' has a list suffix but no values.")
                    : Name;
        }
        else
        {
            var name = Name;
            var nameValueSeparator = DefaultNameValueSeparator;

            // Parse the suffix if specified.
            if (SuffixPattern.Match(Name) is { Success: true, Length: > 0 } suffixMatch)
            {
                if (suffixMatch.Index == 0)
                {
                    throw new InvalidOperationException($"Argument name cannot be empty or consist only of suffix: '{Name}'");
                }

                name = Name[..suffixMatch.Index];
                nameValueSeparator = suffixMatch.GroupValueOrDefault("NameValueSeparator", v => v, DefaultNameValueSeparator);
                var isList = suffixMatch.Groups["ItemList"].Success;
                var itemSeparator = suffixMatch.GroupValueOrDefault("ItemSeparator", v => v, DefaultItemSeparator);

                // List: --tags[] or --tags[,] => --tags a b or --tags a,b
                if (isList)
                {
                    var items = string.Join(itemSeparator, Values);
                    return $"{name}{nameValueSeparator}{items}";
                }
            }

            // Repeated: --file a --file b or --file=a --file=b
            return string.Join(" ", Values.Select(value => $"{name}{nameValueSeparator}{value}"));
        }
    }
}

public static class MatchExtensions
{
    public static T GroupValueOrDefault<T>(this Match match, string groupName, Func<string, T> transform, T defaultValue)
    {
        var group = match.Groups[groupName];
        return group.Success ? transform(group.Value) : defaultValue;
    }
}