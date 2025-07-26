using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Aion.Core;

// core: Represents a single workflow-lock that carries the same name, but a different extension.
public record MaintenancePeriod
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

    public bool IsPending => StartsOnUtc > Clock.GetUtcNow();
    public bool IsExpired => EndsOnUtc < Clock.GetUtcNow();
    public bool IsRunning => StartsOnUtc <= Clock.GetUtcNow() && EndsOnUtc > Clock.GetUtcNow();

    public static MaintenancePeriod StartsAt(DateTimeOffset startsOnUtc, DateTimeOffset endsOnUtc, TimeProvider? clock = null)
    {
        clock ??= TimeProvider.System;
        if (startsOnUtc > endsOnUtc)
        {
            throw new ArgumentException("Workflow lock's start must be before end.", nameof(startsOnUtc));
        }

        if (endsOnUtc < clock.GetUtcNow())
        {
            throw new ArgumentException("Workflow lock's expiry must be in the future.", nameof(endsOnUtc));
        }

        return new MaintenancePeriod
        {
            StartsOnUtc = startsOnUtc,
            EndsOnUtc = endsOnUtc,
        };
    }

    public static MaintenancePeriod StartsIn(TimeSpan wait, TimeSpan length, TimeProvider? clock = null)
    {
        clock ??= TimeProvider.System;
        var startsOnUtc = clock.GetUtcNow().Add(wait);
        var endsOnUtc = startsOnUtc.Add(length);

        return StartsAt(startsOnUtc, endsOnUtc);
    }

    public static async Task<MaintenancePeriod> FromFile(string workflowPath)
    {
        var workflowLockPath = Path.ChangeExtension(workflowPath, FileExtension);

        if (!Path.Exists(workflowLockPath))
        {
            throw new FileNotFoundException($"Workflow lock file '{workflowLockPath}' not found.", fileName: workflowLockPath);
        }

        // core: Avoid race conditions by locking file operations.
        await Lock.WaitAsync();
        try
        {
            await using var fileStream = new FileStream(workflowLockPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (await JsonSerializer.DeserializeAsync<MaintenancePeriod>(fileStream) is { } workflowLock)
            {
                return workflowLock with { FileName = workflowLockPath };
            }
        }
        finally
        {
            Lock.Release();
        }

        throw new WorkflowLockNullException(workflowPath);
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

    public async ValueTask Cancel()
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

public class WorkflowLockNullException(string path) : Exception($"Workflow '{path}' is null.");