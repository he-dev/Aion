using System;
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Quartz;

namespace Aion.Core.Entities;

public record Workflow
{
    public string Name { get; init; } = null!;

    // core: Make the user specify this value explicitly, so they don't activate workflows by accident.
    public bool Enabled { get; init; }

    public Func<DateTimeOffset?, ITrigger> CreateTrigger { get; init; } = null!;

    public IImmutableDictionary<string, string> Variables { get; init; } = null!;

    public IImmutableList<Step> Steps { get; init; } = [];

    public JsonObject? Logging { get; init; }

    public class Step
    {
        public int Index { get; init; }

        public string? Name { get; init; }

        public bool Enabled { get; init; } = true;

        public string FileName { get; init; } = null!;

        // note: Arguments can pass runtime values such as activity ids, so this property must be lazy.
        public Func<string> Arguments { get; init; } = null!;

        public IImmutableDictionary<string, string> Environment { get; init; } = null!;

        public string WorkingDirectory { get; init; } = null!;

        public TimeSpan Timeout { get; init; }

        public JsonObject? Logging { get; init; } = null!;

        public string? DependsOn { get; init; }
    }
}