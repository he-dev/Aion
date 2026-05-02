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
    [AttributeUsage(AttributeTargets.Class)]
    public class ActivityAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Class)]
    public class ChannelAttribute(string? name = null) : Attribute
    {
        public string? Name { get; } = name;
    }

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

    public interface IStatusOnly;

    public interface IStatusWithDuration;
}

public static class FindAttribute
{
    public static Match<TAttribute> Where<TActivity, TAttribute>() where TAttribute : Attribute
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
                return new Match<TAttribute>(attribute, visited.ToArray(), path.ToArray());
            }
        }

        throw new InvalidOperationException($"The '{typeof(TAttribute).Name}' was not found on any of the checked types [{string.Join(", ", visited.Select(t => t.Name))}].");
    }

    public sealed record Match<T>(T Attribute, IReadOnlyList<Type> Visited, IReadOnlyList<Type> Path) where T : Attribute
    {
        public int Depth => Visited.Count;

        public string Name { get; } = string.Join(".", Path.Select(t => t.Name));
    }
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

        public void LogStatus<TActivity>(ActivityStatus<TActivity> status) where TActivity : Telemetry.IStatusOnly
        {
            status.Log(logger.Data, TimeSpan.Zero);
        }
    }

    // note: Without these two concrete extensions, the wrong BeginScope is resolved and BeginScope requires two generic parameters to resolve correctly.

    extension(ILogger<Telemetry.Channel.Engine> logger)
    {
        public ActivityScope<TActivity> BeginScope<TActivity>(params (string Key, object? Value)[] state) where TActivity : Telemetry.IStatusWithDuration
        {
            return ActivityScope<TActivity>.Start(logger, state);
        }
    }

    extension(ILogger<Telemetry.Channel.Output> logger)
    {
        public ActivityScope<TActivity> BeginScope<TActivity>(params (string Key, object? Value)[] state) where TActivity : Telemetry.IStatusWithDuration
        {
            return ActivityScope<TActivity>.Start(logger, state);
        }
    }
}

// core: This class may not be a logger, because it will circumvent the LogStatus constraints for statuses allowing to apply IStatusOnly to an IStatusWithDuration scope!
public class ActivityScope<TActivity> : IDisposable where TActivity : notnull
{
    private static readonly ActivityStatus<TActivity>.First First = new();

    // note: Not using the default constructor because the "state" parameter name clashes with the ILogger interface.
    private ActivityScope(ILogger logger)
    {
        Logger = logger;
        Activity = new Activity(First.Activity).Start();
    }

    private ILogger Logger { get; }

    private Activity Activity { get; }

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

        status.Log(Logger, Activity.Duration);

        return this;
    }

    private void Stop(ActivityStatusCode status)
    {
        if (Activity.IsStopped)
        {
            throw new InvalidOperationException($"The code is trying to log another terminal status for the '{{Activity.OperationName}}' activity, but activities can have only one result.");
        }

        Activity.Stop();
        Activity.SetStatus(status);
    }


    public void Dispose()
    {
        if (!Activity.IsStopped)
        {
            throw new InvalidOperationException($"The '{Activity.OperationName}' activity was started but never concluded. A terminal status (Ok or Error) is missing.");
        }

        Activity.Dispose();
    }

    public static ActivityScope<TActivity> Start<TChannel>(ILogger<TChannel> logger, params (string Key, object? Value)[] items) where TChannel : Telemetry.Channel
    {
        logger = items.Any() ? logger.WithState(items) : logger;
        return new ActivityScope<TActivity>(logger.Data).LogStatus(First);
    }
}

// meta: This class is required to make the message template work with structured logging as the attribute can only be used on parameters.
public record StatusContract([StructuredMessageTemplate] string? Message, params object?[] Args);

public abstract class ActivityStatus<TActivity>
{
    // ReSharper disable once StaticMemberInGenericType - this is intended.
    private static readonly FindAttribute.Match<Telemetry.ChannelAttribute> ChannelMatch;

    // ReSharper disable once StaticMemberInGenericType - this is intended.
    private static readonly FindAttribute.Match<Telemetry.ActivityAttribute> ActivityMatch;

    static ActivityStatus()
    {
        ChannelMatch = FindAttribute.Where<TActivity, Telemetry.ChannelAttribute>();
        ActivityMatch = FindAttribute.Where<TActivity, Telemetry.ActivityAttribute>();

        // core: Making sure the channel comes before the activity.
        if (!(ChannelMatch.Depth > ActivityMatch.Depth))
        {
            throw new InvalidOperationException($"Channels must come before contracts but '{ChannelMatch.Name}' comes after '{ActivityMatch.Name}'.");
        }
    }

    public string Channel => ChannelMatch.Attribute.Name ?? ChannelMatch.Path.First().Name;

    public string Activity => ActivityMatch.Name;

    public abstract string Code { get; }

    public abstract bool IsLast { get; }

    protected abstract StatusContract Render(TimeSpan duration);

    protected abstract void Emit(ILogger logger, StatusContract contract);

    public void Log(ILogger logger, TimeSpan duration)
    {
        var state = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            { nameof(Activity), Activity },
            { nameof(Channel), Channel },
            { nameof(Telemetry.Stream), nameof(Telemetry.Stream.Data) },
        };

        using (logger.BeginScope(state))
        {
            Emit(logger, Render(duration));
        }
    }

    protected static StatusContract Template([StructuredMessageTemplate] string? message, params object?[] args) => new(message, args);

    // core: First status always logs at trace level.
    public class First : ActivityStatus<TActivity>
    {
        public override string Code => nameof(First);

        public override bool IsLast => false;

        protected override StatusContract Render(TimeSpan duration)
        {
            return Template("{Activity}: {Status}", Activity, nameof(First));
        }

        protected override void Emit(ILogger logger, StatusContract contract) => logger.LogTrace(contract.Message, contract.Args);
    }

    // core: Ok status always logs at info level.
    public abstract class Ok : ActivityStatus<TActivity>
    {
        public override string Code => nameof(Ok);

        public override bool IsLast => true;

        protected override void Emit(ILogger logger, StatusContract contract) => logger.LogInformation(contract.Message, contract.Args);
    }

    // core: Error status always logs at error level.
    public abstract class Error : ActivityStatus<TActivity>
    {
        public override string Code => nameof(Error);

        public override bool IsLast => true;

        public Exception? Exception { get; init; }

        protected override void Emit(ILogger logger, StatusContract contract) => logger.LogError(Exception, contract.Message, contract.Args);
    }
}

[Telemetry.Channel]
public abstract class Output
{
    [Telemetry.Activity]
    public abstract class Workflow
    {
        public abstract class ExecuteStep
        {
            public abstract class Now : Telemetry.IStatusWithDuration
            {
                public required int StepIndex { get; init; }

                public sealed class First : ActivityStatus<Now>.First
                {
                    public required int StepIndex { get; init; }

                    protected override StatusContract Render(TimeSpan duration)
                    {
                        return Template("{Activity}: {Status} in {DurationMs:N0} ms; StepIndex: {StepIndex}", Activity, Code, duration.TotalMilliseconds, StepIndex);
                    }
                }

                public sealed class Ok(int stepIndex) : ActivityStatus<Now>.Ok
                {
                    protected override StatusContract Render(TimeSpan duration)
                    {
                        return Template("{Activity}: {Status} in {DurationMs:N0} ms; StepIndex: {StepIndex}", Activity, Code, duration.TotalMilliseconds, stepIndex);
                    }
                }

                public sealed class Error(int stepIndex) : ActivityStatus<Now>.Error
                {
                    protected override StatusContract Render(TimeSpan duration)
                    {
                        return Template("{Activity}: {Status} in {DurationMs:N0} ms; StepIndex: {StepIndex}", Activity, Code, duration.TotalMilliseconds, stepIndex);
                    }
                }
            }
        }
    }
}

[Telemetry.Channel]
public abstract class Engine
{
    [Telemetry.Activity]
    public abstract record DeleteFile : Telemetry.IStatusOnly
    {
        public sealed class Ok(string fileName) : ActivityStatus<DeleteFile>.Ok
        {
            protected override StatusContract Render(TimeSpan duration)
            {
                return Template("{Activity}: {Status}; File: {FileName} ", Activity, Code, fileName);
            }
        }
    }
}

public abstract class Examples
{
    public static void TaskExample()
    {
        var logger = new LoggerFactory().CreateLogger<Examples>();
        using var step = logger.Output.BeginScope<Output.Workflow.ExecuteStep.Now>();
        // busy...
        step.LogStatus(new Output.Workflow.ExecuteStep.Now.Ok(1));
        // step.LogStatus(DeleteFile.Ok("fake.exe")); // core: Compile error because the activity does not match the scope!
        step.LogStatus(new Output.Workflow.ExecuteStep.Now.Error(3) { Exception = new Exception("Fake error") }); // core: This will throw as the activity is already stopped.
    }

    public static void FactExample()
    {
        var logger = new LoggerFactory().CreateLogger<Examples>();
        // busy...
        logger.LogStatus(new Engine.DeleteFile.Ok("fake.exe"));
        logger.Output.LogTrace("Fake trace");
        logger.Output.LogTrace("Fake trace");
        //logger.Output.Note.LogInformation("Fake note");
        //logger.Output.Metric.LogInformation("Fake note");
    }
}