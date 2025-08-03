using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Aion.Core;

// core: Represents a single workflow-lock that carries the same name, but a different extension.
public record TestsMaintenancePeriod
{
    public const string FileExtension = ".lock";

    private static readonly SemaphoreSlim Lock = new(1, 1);

    // util: Provides the means to override it for tests.
    [JsonIgnore]
    public TimeProvider Clock { get; init; } = TimeProvider.System;

    public DateTimeOffset StartsOnUtc { get; init; }
    public DateTimeOffset EndsOnUtc { get; init; }
    public DateTimeOffset CreatedOnUtc { get; init; }

    [JsonIgnore]
    public string? FileName { get; init; }

    public TimeSpan Duration => EndsOnUtc - StartsOnUtc;
    public TimeSpan Remaining => EndsOnUtc - Clock.GetUtcNow();

    public MaintenancePeriodStatus Status
    {
        get
        {
            if (EndsOnUtc <= Clock.GetUtcNow()) return MaintenancePeriodStatus.Expired;
            if (StartsOnUtc <= Clock.GetUtcNow() && EndsOnUtc > Clock.GetUtcNow()) return MaintenancePeriodStatus.Running;
            if (StartsOnUtc > Clock.GetUtcNow()) return MaintenancePeriodStatus.Pending;

            throw new InvalidOperationException("This case is impossible!");
        }
    }

    public static TestsMaintenancePeriod StartsAt(DateTimeOffset startsOnUtc, DateTimeOffset endsOnUtc, TimeProvider? clock = null)
    {
        clock ??= TimeProvider.System;
        if (startsOnUtc > endsOnUtc) throw new MaintenancePeriodMustStartBeforeItEnds();
        if (endsOnUtc < clock.GetUtcNow()) throw new MaintenancePeriodMustEndInTheFuture();

        return new TestsMaintenancePeriod
        {
            StartsOnUtc = startsOnUtc,
            EndsOnUtc = endsOnUtc,
        };
    }

    public static TestsMaintenancePeriod StartsIn(TimeSpan wait, TimeSpan length, TimeProvider? clock = null)
    {
        clock ??= TimeProvider.System;
        var startsOnUtc = clock.GetUtcNow().Add(wait);
        var endsOnUtc = startsOnUtc.Add(length);

        return StartsAt(startsOnUtc, endsOnUtc);
    }

    public static async Task<TestsMaintenancePeriod?> FromFile(string workflowPath)
    {
        var workflowLockPath = Path.ChangeExtension(workflowPath, FileExtension);

        // core: This workflow has no lock.
        if (!Path.Exists(workflowLockPath)) return null;

        // core: Avoid race conditions by locking file operations.
        await Lock.WaitAsync();
        try
        {
            await using var fileStream = new FileStream(workflowLockPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (await JsonSerializer.DeserializeAsync<TestsMaintenancePeriod>(fileStream) is { } workflowLock)
            {
                return workflowLock with { FileName = workflowLockPath };
            }
        }
        finally
        {
            Lock.Release();
        }

        throw new InvalidWorkflowLock(workflowPath);
    }

    public async Task ApplyTo(IEnumerable<string> workflowPaths)
    {
        foreach (var workflowFile in workflowPaths)
        {
            await ToFile(workflowFile);
        }
    }

    public async Task<string> ToFile(string workflowPath)
    {
        var lockPath = Path.ChangeExtension(workflowPath, FileExtension);

        // core: Avoid race conditions by locking file operations.
        await Lock.WaitAsync();
        try
        {
            await using var stream = new FileStream(lockPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, this, new JsonSerializerOptions
            {
                WriteIndented = true,
                IgnoreReadOnlyProperties = true,
            });
            await stream.FlushAsync();
        }
        finally
        {
            Lock.Release();
        }

        return lockPath;
    }

    public async ValueTask Complete()
    {
        // util: Prevent these two bugs that won't happen during normal operation, but only due to mistakes.
        if (FileName is null) throw new InvalidOperationException("Cannot delete a lock that is not saved.");

        if (File.Exists(FileName))
        {
            // core: Avoid race conditions by locking file operations.
            await Lock.WaitAsync();
            try
            {
                File.Delete(FileName);
            }
            finally
            {
                Lock.Release();
            }
        }
    }
}

public enum MaintenancePeriodStatus
{
    Pending,
    Running,
    Expired,
}

public class InvalidWorkflowLock(string path) : Exception($"The '{path}' is not a valid workflow-lock-file.");

public class MaintenancePeriodMustStartBeforeItEnds : Exception;

public class MaintenancePeriodMustEndInTheFuture : Exception;