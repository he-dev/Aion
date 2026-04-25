using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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

public record Overload(string Name)
{
    [return: NotNullIfNotNull("name")]
    public static implicit operator Overload?(string? name) => name is null ? null : new Overload(name);

    public override string ToString() => Name;
}

public record ActivityName<TActivity>(Overload? Overload)
{
    public override string ToString()
    {
        return
            Overload is null
                ? typeof(TActivity).Name
                : $"{typeof(TActivity).Name}.{Overload}";
    }

    public static implicit operator string(ActivityName<TActivity> name) => name.ToString();
}

public static class LoggerExtensions
{
    // note: Channels.
    extension<T>(ILogger<T> logger)
    {
        // core: This is the channel for things that the system produces.
        public ILogger<Telemetry.Channel.Output> Output => new TelemetryLogger<T, Telemetry.Channel.Output>(logger);

        // core: This is the channel for things that enables the system to produce.
        public ILogger<Telemetry.Channel.Engine> Engine => new TelemetryLogger<T, Telemetry.Channel.Engine>(logger);
    }

    // note: Streams.
    extension<T>(ILogger<T> logger)
    {
        public void LogNote([StructuredMessageTemplate] string? message, params object?[] args)
        {
            using (logger.BeginScope(new Dictionary<string, object> { { nameof(Telemetry.Stream), nameof(Telemetry.Stream.Note) } }))
            {
                logger.Log(ILogger<T>.IsChannel() ? LogLevel.Information : LogLevel.Debug, message, args);
            }
        }

        public void LogText([StructuredMessageTemplate] string? message, params object?[] args)
        {
            using (logger.BeginScope(new Dictionary<string, object> { { nameof(Telemetry.Stream), nameof(Telemetry.Stream.Text) } }))
            {
                logger.Log(ILogger<T>.IsChannel() ? LogLevel.Information : LogLevel.Debug, message, args);
            }
        }

        private static bool IsChannel() => typeof(Telemetry.Channel).IsAssignableFrom(typeof(T));
    }

    // meta: This class is used to add a role to a logger.
    // It re-wraps the class-logger into a role-logger.
    // This way we can conveniently chain role-specific extensions.
    private class TelemetryLogger<TLogger, TChannel>(ILogger<TLogger> inner) : ILogger<TChannel> where TChannel : Telemetry.Channel
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return inner.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return inner.IsEnabled(logLevel);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var status = new Dictionary<string, object>
            {
                { nameof(Telemetry.Channel), typeof(TChannel).Name },
            };
            using (BeginScope(status))
            {
                inner.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }

    extension<TContract>(ILogger<TContract> logger) where TContract : Telemetry.Channel
    {
        // core: Logs scope-less activity because their duration does not matter. Usually nearly instant ones. You just want to log their occurrence.
        public void LogStatus<TActivity>(ActivityStatus<TActivity> status, Overload? overload = null)
        {
            status.Log(logger, new ActivityStatusContext(new ActivityName<TActivity>(overload), nameof(Telemetry.Stream.Data)));
        }
    }

    extension(ILogger<Telemetry.Channel.Engine> logger)
    {
        public ActivityScope<TActivity, Telemetry.Channel.Engine> BeginScope<TActivity>(params (string Key, object Value)[] state)
        {
            return ActivityScope<TActivity, Telemetry.Channel.Engine>.Start(logger, null, state);
        }

        public ActivityScope<TActivity, Telemetry.Channel.Engine> BeginScope<TActivity>(Overload overload, params (string Key, object Value)[] state)
        {
            return ActivityScope<TActivity, Telemetry.Channel.Engine>.Start(logger, overload, state);
        }
    }

    extension(ILogger<Telemetry.Channel.Output> logger)
    {
        public ActivityScope<TActivity, Telemetry.Channel.Output> BeginScope<TActivity>(params (string Key, object Value)[] state)
        {
            return ActivityScope<TActivity, Telemetry.Channel.Output>.Start(logger, null, state);
        }

        public ActivityScope<TActivity, Telemetry.Channel.Output> BeginScope<TActivity>(Overload overload, params (string Key, object Value)[] state)
        {
            return ActivityScope<TActivity, Telemetry.Channel.Output>.Start(logger, overload, state);
        }
    }
}

// core: This class, also being a logger, makes it very convenient to use as it does not require to re-implement each logger API.
public class ActivityScope<TActivity, TChannel> : ILogger<TChannel>, IDisposable where TChannel : Telemetry.Channel
{
    // note: Not using the default constructor because the "state" parameter name clashes with the ILogger interface.
    private ActivityScope(ILogger<TChannel> logger, Overload? overload, object? state)
    {
        Logger = logger;
        State = state;
        Activity = new Activity(new ActivityName<TActivity>(overload)).Start();
    }

    private ILogger<TChannel> Logger { get; }

    private object? State { get; }

    private Activity Activity { get; }

    public ActivityScope<TActivity, TChannel> LogStatus(ActivityStatus<TActivity> status)
    {
        if (Activity.IsStopped) throw new InvalidOperationException($"Cannot call {nameof(LogStatus)}() on an already stopped activity.");

        if (status.IsLast)
        {
            Activity.Stop();
            Activity.SetStatus(status.Code);
        }

        using (State is null ? Disposable.Empty : Logger.BeginScope(State))
        {
            status.Log(Logger, new ActivityStatusContext(Activity.OperationName, nameof(Telemetry.Stream.Data))
            {
                DurationMs = Activity.Duration
            });
        }

        return this;
    }

    public void Dispose()
    {
        if (!Activity.IsStopped)
        {
            throw new InvalidOperationException($"Cannot dispose the '{Activity.OperationName}' activity because it is still running.");
        }

        Activity.Dispose();
    }

    public static ActivityScope<TActivity, TChannel> Start(ILogger<TChannel> logger, Overload? overload, params (string Key, object Value)[] state)
    {
        var first = new ActivityStatus<TActivity>.First(activity => new StatusTemplate("{Activity}: {Status}", activity.Activity, nameof(ActivityStatus<>.First)));
        return new ActivityScope<TActivity, TChannel>(logger, overload, state.ToDictionary()).LogStatus(first);
    }

    #region ILogger<TContract>

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return Logger.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return Logger.IsEnabled(logLevel);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        using (State is null ? Disposable.Empty : BeginScope(State))
        {
            Logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }

    #endregion

    private class Disposable : IDisposable
    {
        public static readonly IDisposable Empty = new Disposable();

        public void Dispose() { }
    }
}

public record ActivityStatusContext(string Activity, string Stream)
{
    public TimeSpan DurationMs { get; init; } = TimeSpan.Zero;
}

// meta: This class is required to make the message template work with structured logging as the attribute can only be used on parameters.
public record StatusTemplate([StructuredMessageTemplate] string? Message, params object?[] Args);

public delegate StatusTemplate RenderStatusTemplateFunc(ActivityStatusContext activity);

public delegate void LogAction(ILogger logger, string? message, object?[] args);

public abstract class ActivityStatus<TActivity>(RenderStatusTemplateFunc render, LogAction log)
{
    public abstract bool IsLast { get; }

    public abstract ActivityStatusCode Code { get; }

    public void Log(ILogger logger, ActivityStatusContext context)
    {
        var template = render(context);

        var state = new Dictionary<string, object>
        {
            { nameof(context.Activity), context.Activity },
            { nameof(context.Stream), context.Stream },
        };

        // core: Including a zero-length duration is pointless.
        if (context.DurationMs > TimeSpan.Zero)
        {
            state.Add(nameof(context.DurationMs), context.DurationMs);
        }

        using (logger.BeginScope(state))
        {
            log(logger, template.Message, template.Args);
        }
    }

    // core: First status always logs at trace level.
    public class First(RenderStatusTemplateFunc render)
        : ActivityStatus<TActivity>(render, Microsoft.Extensions.Logging.LoggerExtensions.LogTrace)
    {
        public override bool IsLast => false;

        public override ActivityStatusCode Code => ActivityStatusCode.Unset;
    }

    // core: Ok status always logs at info level.
    public abstract class Ok(RenderStatusTemplateFunc render)
        : ActivityStatus<TActivity>(render, Microsoft.Extensions.Logging.LoggerExtensions.LogInformation)
    {
        public override bool IsLast => true;

        public override ActivityStatusCode Code => ActivityStatusCode.Ok;
    }

    // core: Error status always logs at error level.
    public abstract class Error(RenderStatusTemplateFunc render, Exception? exception)
        : ActivityStatus<TActivity>(render, (logger, message, args) => logger.LogError(exception, message, args))
    {
        public override bool IsLast => true;

        public override ActivityStatusCode Code => ActivityStatusCode.Error;
    }
}

public abstract class ExecuteStep
{
    public class Ok(int stepIndex) : ActivityStatus<ExecuteStep>.Ok(activity =>
        new StatusTemplate("{Activity}: {Status} in {DurationMs:N0} ms; StepIndex: {StepIndex}", activity.Activity, nameof(Ok), activity.DurationMs.TotalMilliseconds, stepIndex));

    public class Error(int stepIndex, Exception? exception) : ActivityStatus<ExecuteStep>.Error(activity =>
        new StatusTemplate("{Activity}: {Status} in {DurationMs:N0} ms; StepIndex: {StepIndex}", activity.Activity, nameof(Error), activity.DurationMs.TotalMilliseconds, stepIndex), exception);
}

public abstract record DeleteFile
{
    public class Ok(string fileName) : ActivityStatus<DeleteFile>.Ok(activity =>
        new StatusTemplate("{Activity}: {Status}; File: {FileName} ", activity.Activity, nameof(Ok), fileName));
}

public abstract class Examples
{
    public static void TaskExample()
    {
        var logger = new LoggerFactory().CreateLogger<Examples>();
        using var step = logger.Output.BeginScope<ExecuteStep>("Now");
        // busy...
        step.LogStatus(new ExecuteStep.Ok(1));
        // step.LogStatus(DeleteFile.Ok("fake.exe")); // core: Compile error because the activity does not match the scope!
        step.LogStatus(new ExecuteStep.Error(3, new Exception("Fake error"))); // core: This will throw as the activity is already stopped.
    }

    public static void FactExample()
    {
        var logger = new LoggerFactory().CreateLogger<Examples>();
        // busy...
        logger.Engine.LogStatus(new DeleteFile.Ok("fake.exe"));
        logger.Output.LogTrace("Fake trace");
        //logger.Output.Note.LogInformation("Fake note");
        //logger.Output.Metric.LogInformation("Fake note");
    }
}