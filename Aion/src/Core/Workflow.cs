using System;
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Aion.Util.Scriban;
using Quartz;

namespace Aion.Core;

public record Workflow
{
    public string Name { get; init; } = null!;

    // core: Make the user specify this value explicitly, so they don't activate workflows by accident.
    public bool Enabled { get; init; }

    public JobKey CronJobKey { get; init; } = null!;

    public ICronTrigger CronTrigger { get; init; } = null!;

    public IImmutableDictionary<string, string> Variables { get; init; } = null!;

    public IImmutableDictionary<string, string> Environment { get; init; } = null!;

    public IImmutableList<Step> Steps { get; init; } = [];

    public JsonObject? Logging { get; init; }

    public class Step(string arguments, IImmutableList<VariableGroup> variables)
    {
        public int Index { get; init; }

        public string? Name { get; init; }

        public bool Enabled { get; init; } = true;

        public string FileName { get; init; } = null!;

        // note: Arguments can pass activity ids which are available only during runtime, so this property must be lazy.
        public string Arguments() => RendersTemplates.In(arguments, variables);

        public IImmutableDictionary<string, string> Environment { get; init; } = null!;

        public string WorkingDirectory { get; init; } = null!;

        public TimeSpan Timeout { get; init; }

        public JsonObject? Logging { get; init; } = null!;

        public string? DependsOn { get; init; }
    }
}