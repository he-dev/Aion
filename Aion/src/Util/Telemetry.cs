using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            using (BeginScope(new Dictionary<string, object> { { nameof(Contract), typeof(TContract).Name } }))
            {
                inner.Log(logLevel, eventId, state, exception, formatter);
            }
        }
    }
}

public static class Telemetry
{
    extension<TActivity, TContract>(ActivityScope<TActivity, TContract> scope) where TContract : Contract
    {
        public void Log<TStatus>(TStatus status) where TStatus : IActivityStatus
        {
            if (scope.Activity.IsStopped) throw new InvalidOperationException("Cannot call Log() on a stopped activity.");

            switch (status)
            {
                case IActivityStart:
                    status.Log(scope, TimeSpan.Zero);
                    break;
                case IActivityOk:
                    scope.Activity.SetStatus(ActivityStatusCode.Ok).Stop();
                    status.Log(scope, scope.Activity.Duration);
                    break;
                case IActivityError:
                    scope.Activity.SetStatus(ActivityStatusCode.Error).Stop();
                    status.Log(scope, scope.Activity.Duration);
                    break;
            }
        }
    }

    extension<TContract>(ILogger<TContract> logger) where TContract : Contract
    {
        public void Log<TStatus>(TStatus status) where TStatus : IActivityStatus
        {
            status.Log(logger, TimeSpan.Zero);
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

    extension(ILogger<Contract.Engine> logger)
    {
        public ActivityScope<TActivity, Contract.Engine> Begin<TActivity>()
        {
            return new ActivityScope<TActivity, Contract.Engine>(logger);
        }
    }

    extension(ILogger<Contract.Output> logger)
    {
        public ActivityScope<TActivity, Contract.Output> Begin<TActivity>()
        {
            return new ActivityScope<TActivity, Contract.Output>(logger);
        }
    }
}

public class ActivityScope<TActivity, TContract>(ILogger<TContract> logger) : IDisposable, ILogger<TContract>
{
    public Activity Activity { get; } = new Activity(typeof(TActivity).Name).Start();

    public void Dispose()
    {
        if (!Activity.IsStopped)
        {
            throw new InvalidOperationException("Cannot dispose an activity that is still running.");
        }

        Activity.Dispose();
    }

    #region ILogger<TContract>

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
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

public interface IActivityStatus
{
    public void Log(ILogger logger, TimeSpan duration);

    public static string NameOf<T>() where T : IActivityStatus => Regex.Replace(typeof(T).Name, "^IActivity", "");
}

public interface IActivityStart : IActivityStatus
{
    // core: Removes duration from the signature.
    void Log(ILogger logger);

    // meta: This is a bridge to the other overload.
    void IActivityStatus.Log(ILogger logger, TimeSpan duration) => Log(logger);
}

public interface IActivityOk : IActivityStatus;

public interface IActivityError : IActivityStatus;

public interface IActivity
{
    public static string NameOf<T>() where T : IActivity => typeof(T).Name;
}

public abstract record ExecuteStep(int StepIndex)
{
    public record Start(int StepIndex) : ExecuteStep(StepIndex), IActivityStart
    {
        public void Log(ILogger logger)
        {
            logger.LogTrace("{Activity}[{StepIndex}]: {Status}", nameof(ExecuteStep), StepIndex, nameof(Start));
        }
    }

    public record Ok(int StepIndex) : ExecuteStep(StepIndex), IActivityOk
    {
        public void Log(ILogger logger, TimeSpan duration)
        {
            logger.LogInformation("{Activity}[{StepIndex}]: {Status} in {Duration:N0} ms.", nameof(ExecuteStep), StepIndex, nameof(Ok), duration);
        }
    }

    public record Error(int StepIndex, Exception? Exception) : ExecuteStep(StepIndex), IActivityError
    {
        public void Log(ILogger logger, TimeSpan duration)
        {
            logger.LogError(Exception, "{Activity}[{StepIndex}]: {Status} in {Duration:N0} ms.", nameof(ExecuteStep), StepIndex, nameof(Error), duration);
        }
    }
}

public abstract record DeleteFile(string FileName)
{
    public record Ok(string FileName) : DeleteFile(FileName), IActivityOk
    {
        public void Log(ILogger logger, TimeSpan duration)
        {
            logger.LogInformation("{Activity}: {Status}; File: {FileName} ", nameof(DeleteFile), nameof(Ok), FileName);
        }
    }
}

public abstract class Examples
{
    public static void TaskExample()
    {
        var logger = new LoggerFactory().CreateLogger<Examples>();
        using var step = logger.Output.Begin<ExecuteStep>();
        // busy...
        step.Log(new ExecuteStep.Start(1));
        step.Log(new ExecuteStep.Ok(1));
        step.Log(new ExecuteStep.Error(3, new Exception("Fake error"))); // core: This will throw as the activity is stopped.
    }

    public static void FactExample()
    {
        var logger = new LoggerFactory().CreateLogger<Examples>();
        // busy...
        logger.Engine.Log(new DeleteFile.Ok("fake.exe"));
        logger.Output.LogTrace("Fake trace");
        logger.Output.Note.LogInformation("Fake note");
        logger.Output.Metric.LogInformation("Fake note");
    }
}