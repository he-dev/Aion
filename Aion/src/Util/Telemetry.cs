using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    public interface IContract;

    public interface IStatusOnly;

    public interface IStatusWithDuration;

    public static class ContractName
    {
        public static string From<TContract>() => From(typeof(TContract));

        public static string From(Type contract)
        {
            var parts = new Stack<string>();

            for (var type = contract; type is not null; type = type.DeclaringType)
            {
                parts.Push(type.Name);

                // core: This is the first part of the contract name.
                if (typeof(IContract).IsAssignableFrom(type))
                {
                    break;
                }
            }

            return string.Join(".", parts);
        }
    }
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
        public void LogStatus<TActivity>(ActivityStatus<TActivity> status) where TActivity : Telemetry.IStatusOnly
        {
            status.Log(logger, new ActivityStatus<TActivity>.Context(Telemetry.ContractName.From<TActivity>(), nameof(Telemetry.Stream.Data)));
        }

    }

    extension<TActivity, TChannel>(ActivityScope<TActivity, TChannel> scope)
        where TActivity : Telemetry.IStatusWithDuration
        where TChannel : Telemetry.Channel
    {
        public void LogStatus(ActivityStatus<TActivity> status)
        {
            if (status.IsLast)
            {
                scope.Stop(status.NativeCode);
            }

            using (scope.State is null ? Disposable.Empty : scope.BeginScope(scope.State))
            {
                status.Log(scope, new ActivityStatus<TActivity>.Context(scope.Activity.OperationName, nameof(Telemetry.Stream.Data))
                {
                    Duration = scope.Activity.Duration
                });
            }
        }
    }

    extension(ILogger<Telemetry.Channel.Engine> logger)
    {
        public ActivityScope<TActivity, Telemetry.Channel.Engine> BeginScope<TActivity>(params (string Key, object Value)[] state) where TActivity : Telemetry.IStatusWithDuration
        {
            return ActivityScope<TActivity, Telemetry.Channel.Engine>.Start(logger, state);
        }
    }

    extension(ILogger<Telemetry.Channel.Output> logger)
    {
        public ActivityScope<TActivity, Telemetry.Channel.Output> BeginScope<TActivity>(params (string Key, object Value)[] state) where TActivity : Telemetry.IStatusWithDuration
        {
            return ActivityScope<TActivity, Telemetry.Channel.Output>.Start(logger, state);
        }
    }

    private class Disposable : IDisposable
    {
        public static readonly IDisposable Empty = new Disposable();

        public void Dispose() { }
    }
}

// core: This class, also being a logger, makes it very convenient to use as it does not require to re-implement each logger API.
public class ActivityScope<TActivity, TChannel> : ILogger<TChannel>, IDisposable
    where TChannel : Telemetry.Channel
    where TActivity : Telemetry.IStatusWithDuration
{
    // note: Not using the default constructor because the "state" parameter name clashes with the ILogger interface.
    private ActivityScope(ILogger<TChannel> logger, object? state)
    {
        Logger = logger;
        State = state;
        Activity = new Activity(Telemetry.ContractName.From<TActivity>()).Start();
    }

    private ILogger<TChannel> Logger { get; }

    public object? State { get; }

    public Activity Activity { get; }

    public ActivityScope<TActivity, TChannel> Stop(ActivityStatusCode status)
    {
        if (Activity.IsStopped)
        {
            throw new InvalidOperationException($"Activity '{Activity.OperationName}' is already stopped, so calling {nameof(LogStatus)}() again is illegal.");
        }

        Activity.Stop();
        Activity.SetStatus(status);

        return this;
    }

    public ActivityScope<TActivity, TChannel> LogStatus_(ActivityStatus<TActivity> status)
    {
        if (Activity.IsStopped)
        {
            throw new InvalidOperationException($"Activity '{Activity.OperationName}' is already stopped, so calling {nameof(LogStatus)}() again is illegal.");
        }

        if (status.IsLast)
        {
            Activity.Stop();
            Activity.SetStatus(status.NativeCode);
        }

        using (State is null ? Disposable.Empty : Logger.BeginScope(State))
        {
            status.Log(Logger, new ActivityStatus<TActivity>.Context(Activity.OperationName, nameof(Telemetry.Stream.Data))
            {
                Duration = Activity.Duration
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

    public static ActivityScope<TActivity, TChannel> Start(ILogger<TChannel> logger, params (string Key, object Value)[] state)
    {
        var first = new ActivityStatus<TActivity>.First();
        return new ActivityScope<TActivity, TChannel>(logger, state.ToDictionary()).LogStatus(first);
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

// meta: This class is required to make the message template work with structured logging as the attribute can only be used on parameters.
public record StatusContract([StructuredMessageTemplate] string? Message, params object?[] Args);

public abstract class ActivityStatus<TActivity>
{
    // todo: this needs a better name but there are two code properties... mhmmm
    public abstract string CustomCode { get; }

    public abstract bool IsLast { get; }

    public abstract ActivityStatusCode NativeCode { get; }

    protected abstract StatusContract Render(Context activity);

    protected abstract void Emit(ILogger logger, StatusContract contract);

    public void Log(ILogger logger, Context context)
    {
        var state = new Dictionary<string, object>
        {
            { nameof(context.Activity), context.Activity },
            { nameof(context.Stream), context.Stream },
        };

        // core: Including a zero-length duration is pointless.
        if (context.Duration > TimeSpan.Zero)
        {
            state.Add(nameof(context.Duration), context.Duration);
        }

        using (logger.BeginScope(state))
        {
            Emit(logger, Render(context));
        }
    }

    protected static StatusContract Template([StructuredMessageTemplate] string? message, params object?[] args) => new(message, args);

    public record Context(string Activity, string Stream)
    {
        // core: Not part of the constructor because optional.
        public TimeSpan Duration { get; init; } = TimeSpan.Zero;
    }

    // core: First status always logs at trace level.
    public class First : ActivityStatus<TActivity>
    {
        public override string CustomCode => nameof(First);

        public override bool IsLast => false;

        public override ActivityStatusCode NativeCode => ActivityStatusCode.Unset;

        protected override StatusContract Render(Context activity)
        {
            return Template("{Activity}: {Status}", activity.Activity, nameof(First));
        }

        protected override void Emit(ILogger logger, StatusContract contract) => logger.LogTrace(contract.Message, contract.Args);
    }

    // core: Ok status always logs at info level.
    public abstract class Ok : ActivityStatus<TActivity>
    {
        public override string CustomCode => nameof(Ok);

        public override bool IsLast => true;

        public override ActivityStatusCode NativeCode => ActivityStatusCode.Ok;

        protected override void Emit(ILogger logger, StatusContract contract) => logger.LogInformation(contract.Message, contract.Args);
    }

    // core: Error status always logs at error level.
    public abstract class Error : ActivityStatus<TActivity>
    {
        public override string CustomCode => nameof(Error);

        public override bool IsLast => true;

        public override ActivityStatusCode NativeCode => ActivityStatusCode.Error;

        public Exception? Exception { get; set; }

        protected override void Emit(ILogger logger, StatusContract contract) => logger.LogError(Exception, contract.Message, contract.Args);
    }
}

public abstract class Contracts
{
    public abstract class Workflow : Telemetry.IContract
    {
        public abstract class ExecuteStep
        {
            public abstract class Now : Telemetry.IStatusWithDuration
            {
                public class Ok(int stepIndex) : ActivityStatus<Now>.Ok
                {
                    protected override StatusContract Render(Context activity)
                    {
                        return Template("{Activity}: {Status} in {DurationMs:N0} ms; StepIndex: {StepIndex}", activity.Activity, CustomCode, activity.Duration, stepIndex);
                    }
                }

                public class Error(int stepIndex) : ActivityStatus<Now>.Error
                {
                    protected override StatusContract Render(Context activity)
                    {
                        return Template("{Activity}: {Status} in {DurationMs:N0} ms; StepIndex: {StepIndex}", activity.Activity, CustomCode, activity.Duration, stepIndex);
                    }
                }
            }
        }
    }

    public abstract record DeleteFile : Telemetry.IContract
    {
        public abstract class Force : Telemetry.IStatusOnly
        {
            public class Ok(string fileName) : ActivityStatus<Force>.Ok
            {
                protected override StatusContract Render(Context activity)
                {
                    return Template("{Activity}: {Status}; File: {FileName} ", activity.Activity, CustomCode, fileName);
                }
            }
        }
    }
}

public abstract class Examples
{
    public static void TaskExample()
    {
        var logger = new LoggerFactory().CreateLogger<Examples>();
        using var step = logger.Output.BeginScope<Contracts.Workflow.ExecuteStep.Now>();
        // busy...
        step.LogStatus(new Contracts.Workflow.ExecuteStep.Now.Ok(1));
        // step.LogStatus(DeleteFile.Ok("fake.exe")); // core: Compile error because the activity does not match the scope!
        step.LogStatus(new Contracts.Workflow.ExecuteStep.Now.Error(3) { Exception = new Exception("Fake error") }); // core: This will throw as the activity is already stopped.
    }

    public static void FactExample()
    {
        var logger = new LoggerFactory().CreateLogger<Examples>();
        // busy...
        logger.Engine.LogStatus(new Contracts.DeleteFile.Force.Ok("fake.exe"));
        logger.Output.LogTrace("Fake trace");
        //logger.Output.Note.LogInformation("Fake note");
        //logger.Output.Metric.LogInformation("Fake note");
    }
}