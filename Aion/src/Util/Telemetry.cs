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

    public interface IResolveContractName
    {
        string From<TContract>() where TContract : notnull;
    }
}

[AttributeUsage(AttributeTargets.Class)]
public class ResolveContractNameByTypeHierarchy : Attribute, Telemetry.IResolveContractName
{
    public string From<TContract>() where TContract : notnull
    {
        var parts = new Stack<string>();

        for (var type = typeof(TContract); type is not null; type = type.DeclaringType)
        {
            parts.Push(type.Name);

            // core: This is the first part of the contract name.
            if (typeof(Telemetry.IContract).IsAssignableFrom(type))
            {
                break;
            }
        }

        return string.Join(".", parts);
    }
}

public static class ResolveContractName
{
    private static Telemetry.IResolveContractName Default { get; } = new ResolveContractNameByTypeHierarchy();

    public static string From<T>() where T : notnull
    {
        for (var type = typeof(T); type is not null; type = type.DeclaringType)
        {
            if (GetResolveContractNameFrom(type) is { } resolveContractName)
            {
                return resolveContractName.From<T>();
            }
        }

        return Default.From<T>();
    }

    private static Telemetry.IResolveContractName? GetResolveContractNameFrom(Type type)
    {
        return
            Attribute
                .GetCustomAttributes(type, inherit: false)
                .OfType<Telemetry.IResolveContractName>()
                .SingleOrDefault();
    }
}

public sealed class LoggerMapping<TFrom, TTo>(ILogger<TFrom> inner) : ILogger<TTo>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        inner.Log(logLevel, eventId, state, exception, formatter);
    }
}

public sealed class LoggerStating<T, TStating>(ILogger inner, TStating stating) : ILogger<T> where TStating : notnull
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        using (inner.BeginScope(stating))
        {
            inner.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}

public static class LoggerExtensions
{
    // note: Channels.
    extension<T>(ILogger<T> logger)
    {
        public ILogger<TOther> MapAs<TOther>() => new LoggerMapping<T, TOther>(logger);

        public ILogger<T> With<TState>(TState state) where TState : notnull => new LoggerStating<T, TState>(logger, state);

        public ILogger<T> With(params (string Key, object? Value)[] state) => new LoggerStating<T, IDictionary<string, object?>>(logger, state.ToDictionary());

        public ILogger<Telemetry.Channel.Output> Output => logger.MapAs<T, Telemetry.Channel.Output>().With((nameof(Telemetry.Channel), nameof(Telemetry.Channel.Output)));
        public ILogger<Telemetry.Channel.Engine> Engine => logger.MapAs<T, Telemetry.Channel.Engine>().With((nameof(Telemetry.Channel), nameof(Telemetry.Channel.Engine)));

        public ILogger<Telemetry.Stream.Data> Data => logger.MapAs<T, Telemetry.Stream.Data>().With((nameof(Telemetry.Stream), nameof(Telemetry.Stream.Data)));
        public ILogger<Telemetry.Stream.Note> Note => logger.MapAs<T, Telemetry.Stream.Note>().With((nameof(Telemetry.Stream), nameof(Telemetry.Stream.Note)));
        public ILogger<Telemetry.Stream.Text> Text => logger.MapAs<T, Telemetry.Stream.Text>().With((nameof(Telemetry.Stream), nameof(Telemetry.Stream.Text)));
    }

    extension<TChannel>(ILogger<TChannel> logger) where TChannel : Telemetry.Channel
    {
        // core: Logs scope-less activity because their duration does not matter. Usually nearly instant ones. You just want to log their occurrence.
        public void LogStatus<TActivity>(ActivityStatus<TActivity> status) where TActivity : Telemetry.IStatusOnly
        {
            var contractName = ResolveContractName.From<TActivity>();
            status.Log(logger.Data, new ActivityStatus<TActivity>.Context(contractName, nameof(Telemetry.Stream.Data)));
        }
    }

    // note: Without these two concrete extensions, the wrong BeginScope is resolved and BeginScope requires two generic parameters to resolve correctly.

    extension(ILogger<Telemetry.Channel.Engine> logger)
    {
        public ActivityScope<TActivity> BeginScope<TActivity>(params (string Key, object Value)[] state) where TActivity : Telemetry.IStatusWithDuration
        {
            return ActivityScope<TActivity>.Start(logger, state);
        }
    }

    extension(ILogger<Telemetry.Channel.Output> logger)
    {
        public ActivityScope<TActivity> BeginScope<TActivity>(params (string Key, object Value)[] state) where TActivity : Telemetry.IStatusWithDuration
        {
            return ActivityScope<TActivity>.Start(logger, state);
        }
    }
}

// core: This class may not be a logger, because it will circumvent the LogStatus constraints for statuses allowing to apply IStatusOnly to an IStatusWithDuration scope!
public class ActivityScope<TActivity> : IDisposable where TActivity : notnull
{
    // note: Not using the default constructor because the "state" parameter name clashes with the ILogger interface.
    private ActivityScope(ILogger logger)
    {
        Logger = logger;
        var contractName = ResolveContractName.From<TActivity>();
        Activity = new Activity(contractName).Start();
    }

    private ILogger Logger { get; }

    private Activity Activity { get; }

    public ActivityScope<TActivity> LogStatus(ActivityStatus<TActivity> status)
    {
        if (status.IsLast)
        {
            Stop(status.NativeCode);
        }

        status.Log(Logger, new ActivityStatus<TActivity>.Context(Activity.OperationName, nameof(Telemetry.Stream.Data))
        {
            Duration = Activity.Duration
        });

        return this;
    }

    private void Stop(ActivityStatusCode status)
    {
        if (Activity.IsStopped)
        {
            throw new InvalidOperationException($"Activity '{Activity.OperationName}' is already stopped, so calling {nameof(LogStatus)}() again is illegal.");
        }

        Activity.Stop();
        Activity.SetStatus(status);
    }


    public void Dispose()
    {
        if (!Activity.IsStopped)
        {
            throw new InvalidOperationException($"Cannot dispose the '{Activity.OperationName}' activity because it is still running.");
        }

        Activity.Dispose();
    }

    public static ActivityScope<TActivity> Start<TChannel>(ILogger<TChannel> logger, params (string Key, object Value)[] state) where TChannel : Telemetry.Channel
    {
        logger = state.Any() ? logger.With(state.ToDictionary()) : logger;
        var first = new ActivityStatus<TActivity>.First();
        return new ActivityScope<TActivity>(logger.Data).LogStatus(first);
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
                public sealed class Ok(int stepIndex) : ActivityStatus<Now>.Ok
                {
                    protected override StatusContract Render(Context activity)
                    {
                        return Template("{Activity}: {Status} in {DurationMs:N0} ms; StepIndex: {StepIndex}", activity.Activity, CustomCode, activity.Duration, stepIndex);
                    }
                }

                public sealed class Error(int stepIndex) : ActivityStatus<Now>.Error
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
            public sealed class Ok(string fileName) : ActivityStatus<Force>.Ok
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
        logger.Output.LogTrace("Fake trace");
        //logger.Output.Note.LogInformation("Fake note");
        //logger.Output.Metric.LogInformation("Fake note");
    }
}