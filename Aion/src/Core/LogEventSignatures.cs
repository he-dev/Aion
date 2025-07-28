using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Aion.Util;
using Aion.Util.Serilog;
using Serilog.Events;

namespace Aion.Core;

public record WorkflowLogEventSignature(string WorkflowName) : ILogEventSignature
{
    private static IImmutableSet<ProcessMessageSource> ConsoleStreamTypes { get; } =
        ImmutableHashSet<ProcessMessageSource>.Empty
            .Add(ProcessMessageSource.StdOut)
            .Add(ProcessMessageSource.StdErr);

    public bool Matches(LogEvent logEvent)
    {
        var containsConsoleStream =
            logEvent.TryGetScalar<string>(nameof(ProcessMessageSource), out var scalar)
            && ConsoleStreamTypes.Contains(Enum.Parse<ProcessMessageSource>(scalar));

        return logEvent.Matches(new Dictionary<string, object>
        {
            [nameof(WorkflowName)] = WorkflowName
        }) && !containsConsoleStream;
    }
}

public record ConsoleLogEventSignature(string WorkflowName, int StepIndex) : ILogEventSignature
{
    public bool Matches(LogEvent logEvent)
    {
        return logEvent.Matches(new Dictionary<string, object>
        {
            [nameof(WorkflowName)] = WorkflowName,
            [nameof(StepIndex)] = StepIndex
        }) && logEvent.Properties.ContainsKey(nameof(ProcessMessageSource));
    }
}