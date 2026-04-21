using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Aion.Meta.Logging;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Aion.Util;

public abstract class Contract
{
    // core: Logs about what the system is supposed to produce.
    public abstract class Output : Contract;

    // core: Logs about what allows the system able to produce.
    public abstract class Engine : Contract;
}

public static class LoggerExtensions
{
    extension<T>(ILogger<T> logger)
    {
        public ILogger<Contract.Output> Output => new TelemetryLogger<T, Contract.Output>(logger);
        public ILogger<Contract.Engine> Engine => new TelemetryLogger<T, Contract.Engine>(logger);
    }

    // meta: This class is used to add a role to a logger.
    // It re-wraps the class-logger into a role-logger.
    // This way we can conveniently chain role-specific extensions.
    private class TelemetryLogger<TLogger, TContract>(ILogger<TLogger> inner) : ILogger<TContract> where TContract : Contract
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
}

public static class Telemetry
{
    public static string CallerToActivityName([CallerMemberName] string? callerName = null)
    {
        if (callerName == null) throw new ArgumentNullException(nameof(callerName));
        if (!callerName.StartsWith("Log")) throw new ArgumentException("Caller's name must start with 'Log'.", nameof(callerName));
        return callerName[3..];
    }

    extension<TContract>(ILogger<TContract> logger) where TContract : Contract
    {
        public IDisposable? BeginContract()
        {
            return logger.BeginScopeFrom(new { Contract = typeof(TContract).Name });
        }

        public void LogInformation([StructuredMessageTemplate] string? message, params object?[] args)
        {
            using (logger.BeginContract())
            {
                logger.Log(LogLevel.Information, message, args);
            }
        }

        public void LogDebug([StructuredMessageTemplate] string? message, params object?[] args)
        {
            using (logger.BeginContract())
            {
                logger.Log(LogLevel.Debug, message, args);
            }
        }

        public void LogTrace([StructuredMessageTemplate] string? message, params object?[] args)
        {
            using (logger.BeginContract())
            {
                logger.Log(LogLevel.Trace, message, args);
            }
        }

        public void LogError(Exception? exception, [StructuredMessageTemplate] string? message, params object?[] args)
        {
            using (logger.BeginContract())
            {
                logger.Log(LogLevel.Error, exception, message, args);
            }
        }
    }

    extension(ILogger<Contract.Engine> logger)
    {
        public ITelemetryScope<TActivity> Begin<TActivity>()
        {
            return TelemetryScope<TActivity>.With(logger);
        }
    }

    extension(ILogger<Contract.Output> logger)
    {
        public ITelemetryScope<TActivity> Begin<TActivity>()
        {
            return TelemetryScope<TActivity>.With(logger);
        }
    }

    public static ActivityStatusCode ToStatusCode(this Exception? exception) => exception switch
    {
        null => ActivityStatusCode.Ok,
        _ => ActivityStatusCode.Error
    };
}

// meta: The generic parameter is a marker for extensions.
// note: Implements the ILogger<Contract> for easier extension chaining.
public interface ITelemetryScope<TActivity> : ILogger<Contract>, IDisposable
{
    public Activity Activity { get; }
    public ITelemetryScope<TActivity> Ok();
    public ITelemetryScope<TActivity> Error();
}

public class TelemetryScope<TActivity>(ILogger<Contract> logger, string? name)
    : ITelemetryScope<TActivity>
{
    public Activity Activity { get; } = new Activity(name ?? typeof(TActivity).Name).Start();

    private ITelemetryScope<TActivity> Start([StructuredMessageTemplate] string? message, params object?[] args)
    {
        if (message is null)
        {
            logger.LogTrace("{ActivityName}: Begin.", Activity.OperationName);
        }
        else
        {
            logger.LogTrace(message, args);
        }

        return this;
    }

    public ITelemetryScope<TActivity> Ok()
    {
        if (Activity.IsStopped) throw new InvalidOperationException("Cannot call Ok() on a stopped activity.");

        Activity.SetStatus(ActivityStatusCode.Ok).Stop();

        return this;
    }

    public ITelemetryScope<TActivity> Error()
    {
        if (Activity.IsStopped) throw new InvalidOperationException("Cannot call Error() on a stopped activity.");

        Activity.SetStatus(ActivityStatusCode.Error).Stop();

        return this;
    }

    public static ITelemetryScope<TActivity> With(ILogger<Contract> logger, string? name = null, [StructuredMessageTemplate] string? message = null, params object?[] args)
    {
        return new TelemetryScope<TActivity>(logger, name).Start(message, args);
    }

    public void Dispose()
    {
        if (!Activity.IsStopped)
        {
            throw new InvalidOperationException("Cannot dispose an activity that is still running.");
        }

        Activity.Dispose();
    }

    #region ILogger<TContract>

    public IDisposable? BeginScope<TState>(TState state)
    {
        return logger.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logger.IsEnabled(logLevel);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        logger.Log(logLevel, eventId, state, exception, formatter);
    }

    #endregion
}

public static class TelemetryContracts
{
    // note: Only for testing purposes. In the actual code this would be the actual micro-service-class.
    public abstract class ExecuteStep;

    extension(ITelemetryScope<ExecuteStep> scope)
    {
        public void LogOk(int index)
        {
            scope.Ok().LogInformation("{ActivityName}[{StepIndex}]: {StatusCode} in {Duration:N0} ms.", scope.Activity.OperationName, index, scope.Activity.Status, scope.Activity.Duration);
        }

        public void LogError(int index, Exception? exception = null)
        {
            scope.Error().LogError(exception, "{ActivityName}[{StepIndex}]: {StatusCode} in {Duration:N0} ms.", scope.Activity.OperationName, index, scope.Activity.Status, scope.Activity.Duration);
        }
    }

    extension(ILogger<Contract.Engine> logger)
    {
        public void LogDeleteFile(string fileName, Exception? exception = null)
        {
            logger.LogInformation("{ActivityName}: {StatusCode}; File: {FileName} ", Telemetry.CallerToActivityName(), exception.ToStatusCode(), fileName);
        }
    }
}

public abstract class Examples
{
    public static void TaskExample()
    {
        var logger = new LoggerFactory().CreateLogger<Examples>();
        using var step = logger.Output.Begin<TelemetryContracts.ExecuteStep>();
        // busy...
        step.LogOk(3);
        step.LogError(3, new Exception("Fake error")); // core: This will throw.
    }

    public static void FactExample()
    {
        var logger = new LoggerFactory().CreateLogger<Examples>();
        // busy...
        logger.Engine.LogDeleteFile("fake.exe");
        logger.Output.LogTrace("Fake trace");
    }
}