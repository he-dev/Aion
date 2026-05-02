using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Aion.Util;

public static class Telemetry
{
    public abstract class Channel
    {
        // core: Logs about what the system is supposed to produce.
        public abstract class Output : Channel;

        // core: Logs about what allows the system able to produce.
        public abstract class Engine : Channel;
    }

    public abstract class Stream
    {
        // core: Pure telemetry data.
        public abstract class Data;

        // core: Something nice to know about what is going on.
        public abstract class Note;

        // core: Readable entries meant for the console.
        public abstract class Text;
    }
}

public interface IAllowsInconclusiveStatusOnDispose;

[AttributeUsage(AttributeTargets.Class)]
public class ChannelAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = name;
}

public interface IActivityState
{
    public IEnumerable<KeyValuePair<string, object>> EnumerateStateItems();
}

public static class Find<TAttribute> where TAttribute : Attribute
{
    public static AttributeMatch<TAttribute> From<TActivity>()
    {
        var path = new Stack<Type>();
        var visited = new List<Type>();

        for (var current = typeof(TActivity); current is not null; current = current.DeclaringType)
        {
            path.Push(current);
            visited.Add(current);

            // core: Collecting only the first attribute of each type.
            if (current.GetCustomAttribute<TAttribute>(inherit: false) is { } attribute)
            {
                return new AttributeMatch<TAttribute>(attribute, visited.ToArray(), path.ToArray());
            }
        }

        throw new InvalidOperationException($"The '{typeof(TAttribute).Name}' was not found on any of the checked types [{string.Join(", ", visited.Select(t => t.Name))}].");
    }
}

public sealed record AttributeMatch<TAttribute>(TAttribute Attribute, IReadOnlyList<Type> Visited, IReadOnlyList<Type> Path)
{
    public int Depth => Visited.Count;
}

public static class FindChannel
{
    public static ChannelMatch From<TActivity>()
    {
        var path = new Stack<Type>();
        var visited = new List<Type>();

        for (var current = typeof(TActivity); current is not null; current = current.DeclaringType)
        {
            path.Push(current);
            visited.Add(current);

            // core: Stop at the first type assignable to Channel.
            if (current != typeof(Telemetry.Channel) && typeof(Telemetry.Channel).IsAssignableFrom(current))
            {
                return new(current, visited.ToArray(), path.ToArray());
            }
        }

        throw new InvalidOperationException(
            $"The activity '{typeof(TActivity).FullName}' has no channel. " +
            $"One of its declaring types must derive from '{nameof(Telemetry.Channel)}'. " +
            $"Checked: [{string.Join(", ", visited.Select(t => t.Name))}].");
    }
}

public sealed record ChannelMatch(Type Type, IReadOnlyList<Type> Visited, IReadOnlyList<Type> Path)
{
    public int Depth => Visited.Count;

    // core: The channel's own name unless an AliasAttribute renames it.
    public string Name => Type.GetCustomAttribute<AliasAttribute>(inherit: false)?.Name ?? Type.Name;
}

// util: Can be used to override the default name.
[AttributeUsage(AttributeTargets.Class)]
public class AliasAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

public sealed class LoggerProxy<T>(ILogger inner, IEnumerable<KeyValuePair<string, object?>> items) : ILogger<T>
{
    private ILogger Inner { get; } = inner;

    private IImmutableDictionary<string, object?> Items { get; } = items.ToImmutableDictionary();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => Inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => Inner.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (Items.Count == 0)
        {
            Inner.Log(logLevel, eventId, state, exception, formatter);
        }
        else
        {
            using (BeginScope(Items))
            {
                Inner.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }

    public static ILogger<TOther> As<TOther>(ILogger<T> logger)
    {
        if (typeof(T) == typeof(TOther)) throw new ArgumentException($"The code is trying to map the {typeof(T).Name} logger to the same type it already has. Either the type argument is wrong or the mapping is unnecessary.", nameof(TOther));

        return
            logger is LoggerProxy<T> proxy
                ? new LoggerProxy<TOther>(proxy.Inner, proxy.Items)
                : new LoggerProxy<TOther>(logger, ImmutableDictionary<string, object?>.Empty);
    }

    public static ILogger<T> With(ILogger<T> logger, params (string Key, object? Value)[] items)
    {
        if (items.Length == 0) throw new ArgumentException($"The code is trying to extend the {typeof(T).Name} logger scope with no items. Either the call is unnecessary or the items array was not populated correctly.", nameof(items));

        var keyValuePairs = items.Select(item => new KeyValuePair<string, object?>(item.Key, item.Value));

        return
            logger is LoggerProxy<T> proxy
                ? new LoggerProxy<T>(proxy.Inner, proxy.Items.SetItems(keyValuePairs))
                : new LoggerProxy<T>(logger, keyValuePairs);
    }
}

public static class LoggerProxyExtensions
{
    extension<T>(ILogger<T> logger)
    {
        public ILogger<TOther> MapAs<TOther>() => LoggerProxy<T>.As<TOther>(logger);

        public ILogger<T> WithState(params (string Key, object? Value)[] items) => LoggerProxy<T>.With(logger, items);
    }
}

public static class LoggerExtensions
{
    extension<T>(ILogger<T> logger)
    {
        public ILogger<TChannel> Channel<TChannel>() => logger.MapAs<T, TChannel>().WithState((nameof(Telemetry.Channel), typeof(TChannel).Name));

        public ILogger<Telemetry.Channel.Output> Output => logger.Channel<T, Telemetry.Channel.Output>();
        public ILogger<Telemetry.Channel.Engine> Engine => logger.Channel<T, Telemetry.Channel.Engine>();

        public ILogger<Telemetry.Stream.Data> Data => logger.MapAs<T, Telemetry.Stream.Data>().WithState((nameof(Telemetry.Stream), nameof(Telemetry.Stream.Data)));
        public ILogger<Telemetry.Stream.Note> Note => logger.MapAs<T, Telemetry.Stream.Note>().WithState((nameof(Telemetry.Stream), nameof(Telemetry.Stream.Note)));
        public ILogger<Telemetry.Stream.Text> Text => logger.MapAs<T, Telemetry.Stream.Text>().WithState((nameof(Telemetry.Stream), nameof(Telemetry.Stream.Text)));

        public ActivityScope<TActivity> Begin<TActivity>(TActivity activity) where TActivity : IActivity
        {
            return ActivityScope<TActivity>.Start(logger, activity);
        }
    }
}

// core: This class may not be a logger, because it will circumvent the LogStatus constraints for statuses allowing to apply IStatusOnly to an IStatusWithDuration scope!
public class ActivityScope<TActivity>(ILogger logger, TActivity activity) : IDisposable where TActivity : IActivity
{
    private Activity Activity { get; } = new Activity(activity.Name).Start();

    public ActivityScope<TActivity> LogStatus(ActivityStatus<TActivity> status)
    {
        if (status.IsLast)
        {
            var statusCode = status switch
            {
                ActivityStatus<TActivity>.Ok => ActivityStatusCode.Ok,
                ActivityStatus<TActivity>.Error => ActivityStatusCode.Error,
                _ => ActivityStatusCode.Unset
            };

            Stop(statusCode);
        }

        status.Log(logger, activity, Activity.Duration);

        return this;
    }

    private void Stop(ActivityStatusCode status)
    {
        if (Activity.IsStopped)
        {
            throw new InvalidOperationException($"The code is trying to log another terminal status for the '{Activity.OperationName}' activity, but activities can have only one result.");
        }

        Activity.Stop();
        Activity.SetStatus(status);
    }


    public void Dispose()
    {
        if (!Activity.IsStopped)
        {
            if (activity is IAllowsInconclusiveStatusOnDispose)
            {
                LogStatus(new ActivityStatus<TActivity>.Inconclusive());
            }
            else
            {
                throw new InvalidOperationException($"The '{activity.Name}' activity was started but never concluded. A terminal status (Ok or Error) is missing.");
            }
        }

        Activity.Dispose();
    }

    public static ActivityScope<TActivity> Start<T>(ILogger<T> logger, TActivity activity)
    {
        return new ActivityScope<TActivity>(logger, activity).LogStatus(new ActivityStatus<TActivity>.First());
    }
}

// meta: This class is required to make the message template work with structured logging as the attribute can only be used on parameters.
public record StatusTemplate([StructuredMessageTemplate] string? Message, params object?[] Args);

public interface IActivity
{
    public string Channel { get; }

    public string Name { get; }
}

public abstract class Activity<TActivity> : IActivity where TActivity : notnull
{
    private static readonly AttributeMatch<ChannelAttribute> ChannelMatch = Find<ChannelAttribute>.From<TActivity>();

    public string Channel => ChannelMatch.Attribute.Name ?? ChannelMatch.Path.First().Name;

    // note: The activity name begins after the channel, so skip it.
    public string Name => string.Join(".", ChannelMatch.Path.Skip(1).Select(t => t.Name));
}

public abstract class ActivityStatus<TActivity> where TActivity : IActivity
{
    public abstract string Code { get; }

    public abstract bool IsLast { get; }

    // core: Let inheritors provide their own template.
    protected virtual StatusTemplate Render(TActivity activity, TimeSpan duration)
    {
        return Template("{Activity}: {Status} in {DurationMs:N0} ms", activity.Name, Code, duration.TotalMilliseconds);
    }

    // core: Each status needs to provide its own logging.
    protected abstract void Log(ILogger logger, StatusTemplate template);

    public void Log(ILogger logger, TActivity activity, TimeSpan duration)
    {
        var state = new Dictionary<string, object>
        {
            { nameof(Activity), activity.Name },
            { nameof(Telemetry.Channel), activity.Channel },
            { nameof(Telemetry.Stream), nameof(Telemetry.Stream.Data) }
        };

        MergeStateItems(activity, state);
        MergeStateItems(this, state);

        using (logger.BeginScope(state))
        {
            Log(logger, Render(activity, duration));
        }
    }

    private static void MergeStateItems<T>(T source, IDictionary<string, object> state)
    {
        if (source is IActivityState customState)
        {
            var any = false;

            // core: Adding the custom state items to the scope.
            foreach (var (key, value) in customState.EnumerateStateItems())
            {
                if (state.TryGetValue(key, out var currentValue))
                {
                    throw new InvalidOperationException($"The type '{typeof(T).FullName}' tries to add the key '{key}' with value '{value}', but it already exists with value '{currentValue}'.");
                }

                state.Add(key, value);
                any = true;
            }

            if (!any)
            {
                throw new InvalidOperationException($"The type '{typeof(T).FullName}' implements the '{nameof(IActivityState)}' interface but returns zero items.");
            }
        }
    }

    // util: Just some handy helper.
    protected static StatusTemplate Template([StructuredMessageTemplate] string? message, params object?[] args) => new(message, args);

    // core: First status always logs at trace level.
    public class First : ActivityStatus<TActivity>
    {
        public override string Code => nameof(First);

        public override bool IsLast => false;

        protected override void Log(ILogger logger, StatusTemplate template) => logger.LogTrace(template.Message, template.Args);
    }

    public abstract class Halt : ActivityStatus<TActivity>
    {
        public override string Code => nameof(Halt);

        public override bool IsLast => true;

        protected override void Log(ILogger logger, StatusTemplate template) => logger.LogWarning(template.Message, template.Args);
    }

    // core: Ok status always logs at info level.
    public abstract class Ok : ActivityStatus<TActivity>
    {
        public override string Code => nameof(Ok);

        public override bool IsLast => true;

        protected override void Log(ILogger logger, StatusTemplate template) => logger.LogInformation(template.Message, template.Args);
    }

    // core: Error status always logs at error level.
    public abstract class Error : ActivityStatus<TActivity>
    {
        public override string Code => nameof(Error);

        public override bool IsLast => true;

        public Exception? Exception { get; init; }

        protected override void Log(ILogger logger, StatusTemplate template) => logger.LogError(Exception, template.Message, template.Args);
    }

    public class Inconclusive : ActivityStatus<TActivity>
    {
        public override string Code => nameof(Inconclusive);

        public override bool IsLast => true;

        protected override void Log(ILogger logger, StatusTemplate template) => logger.LogWarning(template.Message, template.Args);
    }
}

[Channel]
public abstract class Output : Telemetry.Channel.Output
{
    public abstract class Workflow
    {
        public abstract class ExecuteStep
        {
            public class Now : Activity<Now>, IActivityState
            {
                public required int StepIndex { get; init; }

                public IEnumerable<KeyValuePair<string, object>> EnumerateStateItems()
                {
                    yield return new(nameof(StepIndex), StepIndex);
                }

                public sealed class Ok : ActivityStatus<Now>.Ok, IActivityState
                {
                    // note: Can be either a property or a constructor parameter. Does not really make any difference.
                    public required int ItemsProcessed { get; init; }

                    public IEnumerable<KeyValuePair<string, object>> EnumerateStateItems()
                    {
                        yield return new(nameof(ItemsProcessed), ItemsProcessed);
                    }
                }

                public sealed class Error : ActivityStatus<Now>.Error;
            }
        }
    }
}

[Channel]
public abstract class Engine : Telemetry.Channel.Engine
{
    public class DeleteFile : Activity<DeleteFile>, IActivityState, IAllowsInconclusiveStatusOnDispose
    {
        public required string Path { get; init; }

        public IEnumerable<KeyValuePair<string, object>> EnumerateStateItems()
        {
            yield return new(nameof(Path), Path);
        }

        public sealed class Ok : ActivityStatus<DeleteFile>.Ok;
    }
}

public abstract class Examples
{
    public static void TaskExample()
    {
        var logger = new LoggerFactory().CreateLogger<Examples>();
        using var step = logger.Begin(new Output.Workflow.ExecuteStep.Now { StepIndex = 1 });
        // busy...
        step.LogStatus(new Output.Workflow.ExecuteStep.Now.Ok { ItemsProcessed = 100 });
        // step.LogStatus(DeleteFile.Ok("fake.exe")); // core: Compile error because the activity does not match the scope!
        step.LogStatus(new Output.Workflow.ExecuteStep.Now.Error { Exception = new Exception("Fake error") }); // core: This will throw as the activity is already stopped.
    }

    public static void FactExample()
    {
        var logger = new LoggerFactory().CreateLogger<Examples>();
        // busy...
        logger.Output.LogTrace("Fake trace");
        logger.Output.LogTrace("Fake trace");
        //logger.Output.Note.LogInformation("Fake note");
        //logger.Output.Metric.LogInformation("Fake note");
    }
}