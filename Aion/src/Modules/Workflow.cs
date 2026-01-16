using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Aion.Modules.Logging;
using Quartz;

namespace Aion.Modules;

public record Workflow
{
    public string Profile { get; set; } = null!;

    // core: Make the user specify this value explicitly, so they don't activate workflows by accident.
    public bool Enabled { get; init; }

    public WorkflowMode Mode => Trigger is ICronTrigger ? WorkflowMode.Cron : WorkflowMode.User;

    public string Name { get; init; } = null!;

    public string Path { get; init; } = null!;

    public ITrigger Trigger { get; init; } = null!;

    public IImmutableDictionary<string, string> Variables { get; init; } = null!;

    public IImmutableList<Step> Steps { get; init; } = [];

    public JsonObject? Logging { get; init; }

    public record Step
    {
        public int Index { get; init; }

        public int Order { get; set; }

        public string? Name { get; init; }

        public bool Enabled { get; init; } = true;

        public string FileName { get; init; } = null!;

        // note: Arguments can pass runtime values such as activity ids, so this property must be lazy.
        public Func<IEnumerable<StepArgument>> Arguments { get; init; } = null!;

        public IImmutableDictionary<string, string> Environment { get; init; } = null!;

        public string WorkingDirectory { get; init; } = null!;

        public TimeSpan Timeout { get; init; }

        public JsonObject? Logging { get; init; }

        public string? OnFailure { get; init; }

        public string? DependsOn { get; init; }

        public LoggingTarget LoggingTarget { get; init; }
    }
}