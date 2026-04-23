using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Aion.Util;

public abstract class Channel
{
    // core: Logs about what the system is supposed to produce.
    public abstract class Output : Channel;

    // core: Logs about what allows the system able to produce.
    public abstract class Engine : Channel;
}

public static class LoggerExtensions
{
    extension<T>(ILogger<T> logger)
    {
        // core: This is channel for things that the system produces.
        public ILogger<Channel.Output> Output => new TelemetryLogger<T, Channel.Output>(logger);

        // core: This is channel for things that enables the system to produce.
        public ILogger<Channel.Engine> Engine => new TelemetryLogger<T, Channel.Engine>(logger);
    }

    // meta: This class is used to add a role to a logger.
    // It re-wraps the class-logger into a role-logger.
    // This way we can conveniently chain role-specific extensions.
    private class TelemetryLogger<TLogger, TChannel>(ILogger<TLogger> inner) : ILogger<TChannel> where TChannel : Channel
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
                { nameof(Channel), typeof(TChannel).Name },
            };
            using (BeginScope(status))
            {
                inner.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}

public static class Telemetry
{
    extension<TActivity, TChannel>(ActivityScope<TActivity, TChannel> scope) where TChannel : Channel { }

    extension<TContract>(ILogger<TContract> logger) where TContract : Channel
    {
        // core: Use BeginScope<TActivity>() to log this status with duration.
        public void LogStatus<TStatus>(TStatus status) where TStatus : IActivity, IActivityStatus
        {
            if (typeof(TStatus).BaseType is { } activityType && typeof(IActivity).IsAssignableFrom(activityType))
            {
                status.Log(logger, new ActivityContext(activityType.Name, typeof(TStatus).Name, TimeSpan.Zero));
            }
            else
            {
                throw new InvalidOperationException($"The status type '{typeof(TStatus).Name}' must be derived from {nameof(IActivity)}.");
            }
        }

        public void LogInformation([StructuredMessageTemplate] string? message, params object?[] args)
        {
            logger.Log(LogLevel.Information, message, args);
        }

        public void LogDebug([StructuredMessageTemplate] string? message, params object?[] args)
        {
            logger.Log(LogLevel.Debug, message, args);
        }

        public void LogTrace([StructuredMessageTemplate] string? message, params object?[] args)
        {
            logger.Log(LogLevel.Trace, message, args);
        }

        public void LogError(Exception? exception, [StructuredMessageTemplate] string? message, params object?[] args)
        {
            logger.Log(LogLevel.Error, exception, message, args);
        }
    }

    extension(ILogger<Channel.Engine> logger)
    {
        public ActivityScope<TActivity, Channel.Engine> BeginScope<TActivity>(object? state = null)
        {
            return ActivityScope<TActivity, Channel.Engine>.Start(logger, state);
        }
    }

    extension(ILogger<Channel.Output> logger)
    {
        public ActivityScope<TActivity, Channel.Output> BeginScope<TActivity>(object? state = null)
        {
            return ActivityScope<TActivity, Channel.Output>.Start(logger, state);
        }
    }
}

public class ActivityScope<TActivity, TChannel> : ILogger<TChannel>, IDisposable where TChannel : Channel
{
    // note: Not using the default constructor because the state parameter name clashes with the ILogger interface.
    private ActivityScope(ILogger<TChannel> logger, object? state)
    {
        Logger = logger;
        State = state;
        Activity = new Activity(typeof(TActivity).Name).Start();
    }

    private ILogger<TChannel> Logger { get; }

    private object? State { get; }

    private Activity Activity { get; }

    public void LogStatus<TStatus>(TStatus status) where TStatus : IActivityStatus
    {
        if (Activity.IsStopped) throw new InvalidOperationException("Cannot call LogStatus() on a stopped activity.");

        var statusCode = status switch
        {
            IActivityOk => ActivityStatusCode.Ok,
            IActivityError => ActivityStatusCode.Error,
            // meta: Won't ever happen (if not abused), but makes the compiler happy. Unfortunately, C# does not support exhaustive types like Kotlin does.
            _ => throw new InvalidOperationException($"Unsupported activity status type '{status.GetType().Name}'.")
        };

        Activity.SetStatus(statusCode).Stop();

        // core: Add activity properties to the scope automatically.
        var state = new Dictionary<string, object>
        {
            { nameof(Activity), Activity.OperationName },
            { nameof(Activity.Status), Activity.Status },
            { nameof(Activity.Duration), Activity.Duration }
        };
        using (BeginScope(state))
        {
            status.Log(this, new ActivityContext(Activity.OperationName, Activity.Status.ToString(), Activity.Duration));
        }
    }

    public void Dispose()
    {
        if (!Activity.IsStopped)
        {
            throw new InvalidOperationException("Cannot dispose an activity that is still running.");
        }

        Activity.Dispose();
    }

    public static ActivityScope<TActivity, TChannel> Start(ILogger<TChannel> logger, object? state = null)
    {
        var activity = new ActivityScope<TActivity, TChannel>(logger, state);
        // note: Use the activity's Log to properly handle the scope.
        activity.Log(LogLevel.Trace, "{Activity}: {Status}", typeof(TActivity).Name, nameof(Start));
        return activity;
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

public record ActivityContext(string Name, string Status, TimeSpan Duration);

public interface IActivityStatus
{
    public void Log(ILogger logger, ActivityContext activity);

    public static string NameOf<T>() where T : IActivityStatus => Regex.Replace(typeof(T).Name, "^IActivity", "");
}

public interface IActivityOk : IActivityStatus;

public interface IActivityError : IActivityStatus;

public interface IActivity;

public abstract record ExecuteStep(int StepIndex) : IActivity
{
    public record Ok(int StepIndex) : ExecuteStep(StepIndex), IActivityOk
    {
        public void Log(ILogger logger, ActivityContext activity)
        {
            logger.LogInformation("{Activity}[{StepIndex}]: {Status} in {Duration:N0} ms.", activity.Name, StepIndex, activity.Status, activity.Duration);
        }
    }

    public record Error(int StepIndex, Exception? Exception) : ExecuteStep(StepIndex), IActivityError
    {
        public void Log(ILogger logger, ActivityContext activity)
        {
            logger.LogError(Exception, "{Activity}[{StepIndex}]: {Status} in {Duration:N0} ms.", activity.Name, StepIndex, activity.Status, activity.Duration);
        }
    }
}

public abstract record DeleteFile(string FileName) : IActivity
{
    public record Ok(string FileName) : DeleteFile(FileName), IActivityOk
    {
        public void Log(ILogger logger, ActivityContext activity)
        {
            logger.LogInformation("{Activity}: {Status}; File: {FileName} ", activity.Name, activity.Status, FileName);
        }
    }
}

public abstract class Examples
{
    public static void TaskExample()
    {
        var logger = new LoggerFactory().CreateLogger<Examples>();
        using var step = logger.Output.BeginScope<ExecuteStep>();
        // busy...
        step.LogStatus(new ExecuteStep.Ok(1));
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