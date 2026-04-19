using System;
using System.Diagnostics;
using Aion.Meta.Logging;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Aion.Util;

public abstract class Role
{
    public abstract class Core : Role;

    public abstract class Util : Role;

    public abstract class Meta : Role;
}

public class TelemetryLogger<TLogger, TRole>(ILogger<TLogger> inner) : ILogger<TRole> where TRole : Role
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
        inner.Log(logLevel, eventId, state, exception, formatter);
    }
}

public static class TelemetryLogger
{
    public static ILogger<TRole> AsRole<TLogger, TRole>(this ILogger<TLogger> logger) where TRole : Role
    {
        return new TelemetryLogger<TLogger, TRole>(logger);
    }
}

// core: This class encapsulates the three classes of logging.
public class Telemetry<T>(ILoggerFactory loggerFactory)
{
    public ILogger<Role.Core> Core { get; } = loggerFactory.CreateLogger<T>().AsRole<T, Role.Core>();
    public ILogger<Role.Util> Util { get; } = loggerFactory.CreateLogger<T>().AsRole<T, Role.Util>();
    public ILogger<Role.Meta> Meta { get; } = loggerFactory.CreateLogger<T>().AsRole<T, Role.Meta>();
}

public static class Telemetry
{
    public static ITelemetryTask LogTask<T>(ILogger<T> logger, string name, Action<TelemetryTaskInfo<T>> log) where T : Role
    {
        //return new TelemetryTask<T>(logger, name, onDone);
        return TelemetryTask<T>.From(logger, name, log);
    }

    public static void LogFact<T>(ILogger<T> logger, Exception? exception, [StructuredMessageTemplate] string? message, params object?[] args) where T : Role
    {
        using (logger.BeginScopeFrom(new { Role = typeof(T).Name }))
        {
            if (exception is null)
            {
                logger.LogInformation(message, args);
            }
            else
            {
                logger.LogError(exception, message, args);
            }
        }
    }

    public static void LogNote<T>(this ILogger<T> logger, [StructuredMessageTemplate] string? message, params object?[] args) where T : Role
    {
        using (logger.BeginScopeFrom(new { Role = typeof(T).Name }))
        {
            if (typeof(T) == typeof(Role.Util))
            {
                logger.LogDebug(message, args);
            }
            else
            {
                logger.LogInformation(message, args);
            }
        }
    }

    public static ActivityStatusCode ToStatusCode(this Exception? exception) => exception switch
    {
        null => ActivityStatusCode.Ok,
        _ => ActivityStatusCode.Error
    };
}

public interface ITelemetryTask : IDisposable
{
    public Activity Activity { get; }
    public void LogOk();
    public void LogError(Exception? exception = null);
}

public class TelemetryTask<T>(ILogger logger, string name, Action<TelemetryTaskInfo<T>> log) : ITelemetryTask where T : Role
{
    public Activity Activity { get; } = new(name);

    private ITelemetryTask Start()
    {
        using (logger.BeginScopeFrom(new { Role = typeof(T).Name }))
        {
            log(new TelemetryTaskInfo<T>(logger, Activity.Start()));
        }

        return this;
    }

    public void LogOk()
    {
        if (Activity.IsStopped) throw new InvalidOperationException("Cannot call Ok() on a stopped activity.");

        Activity.SetStatus(ActivityStatusCode.Ok).Stop();
        using (logger.BeginScopeFrom(new { Role = typeof(T).Name }))
        {
            log(new TelemetryTaskInfo<T>(logger, Activity));
        }
    }

    public void LogError(Exception? exception = null)
    {
        if (Activity.IsStopped) throw new InvalidOperationException("Cannot call Error() on a stopped activity.");

        Activity.SetStatus(ActivityStatusCode.Error).Stop();
        using (logger.BeginScopeFrom(new { Role = typeof(T).Name }))
        {
            log(new TelemetryTaskInfo<T>(logger, Activity, exception));
        }
    }

    public static ITelemetryTask From(ILogger logger, string name, Action<TelemetryTaskInfo<T>> onDone)
    {
        return new TelemetryTask<T>(logger, name, onDone).Start();
    }

    public void Dispose()
    {
        if (!Activity.IsStopped)
        {
            throw new InvalidOperationException("Cannot dispose an activity that is still running.");
        }

        Activity.Dispose();
    }
}

// meta: Helper class that allows using the task result in a structured way.
public class TelemetryTaskInfo<T>(ILogger logger, Activity activity, Exception? exception = null) where T : Role
{
    public Activity Activity => activity;

    public Exception? Exception => exception;

    public void Log([StructuredMessageTemplate] string? message, params object?[] args)
    {
        // core: The activity has just started, so its status is unset.
        if (Activity.Status == ActivityStatusCode.Unset)
        {
            logger.LogTrace(message, args);
        }
        else
        {
            if (exception is null)
            {
                if (typeof(T) == typeof(Role.Util))
                {
                    logger.LogDebug(message, args);
                }
                else
                {
                    logger.LogInformation(message, args);
                }
            }
            else
            {
                logger.LogError(exception, message, args);
            }
        }
    }
}

public static class CoreTasks
{
    public static ITelemetryTask LogExecuteStep(this ILogger<Role.Core> logger, int index)
    {
        return Telemetry.LogTask(logger, "ExecuteStep", task =>
        {
            // ...
            task.Log("Execute step {Index}: {Status} in {Duration:N0} ms.", index, task.Activity.Status, task.Activity.Duration);
        });
    }
}

public static class MetaFacts
{
    public static void LogDeleteFile(this ILogger<Role.Meta> logger, string name, Exception? exception = null)
    {
        Telemetry.LogFact(logger, exception, "Delete file '{name}': {Status}", name, exception.ToStatusCode());
    }
}

public abstract class Examples
{
    public static void TaskExample()
    {
        var telemetry = new Telemetry<Examples>(new LoggerFactory());
        using var step = telemetry.Core.LogExecuteStep(5);
        // busy...
        step.LogOk();
    }

    public static void FactExample()
    {
        var telemetry = new Telemetry<Examples>(new LoggerFactory());
        // busy...
        telemetry.Meta.LogDeleteFile("fake.exe");
    }
}