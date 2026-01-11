using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Aion.Util.Core;

// core: Represents a single workflow-lock that carries the same name, but a different extension.
public record WorkflowDowntime
{
    public const string FileExtension = ".lock";

    private static readonly SemaphoreSlim Lock = new(1, 1);

    // util: Provides the means to override it for tests.
    [JsonIgnore]
    public TimeProvider Clock { get; init; } = TimeProvider.System;

    public DateTimeOffset StartsOnUtc { get; init; }
    public DateTimeOffset EndsOnUtc { get; init; }
    public DateTimeOffset CreatedOnUtc { get; init; }

    public string Checksum { get; init; } = null!;

    [JsonIgnore]
    public string? FileName { get; init; }

    public TimeSpan Duration => EndsOnUtc - StartsOnUtc;

    [JsonIgnore]
    public TimeSpan Remaining => EndsOnUtc - Clock.GetUtcNow();

    [JsonIgnore]
    public WorkflowDowntimeStatus Status
    {
        get
        {
            if (StartsOnUtc > Clock.GetUtcNow()) return WorkflowDowntimeStatus.Pending;
            if (StartsOnUtc <= Clock.GetUtcNow() && EndsOnUtc > Clock.GetUtcNow()) return WorkflowDowntimeStatus.Ongoing;
            if (EndsOnUtc <= Clock.GetUtcNow()) return WorkflowDowntimeStatus.Expired;

            // meta: Makes the compiler happy.
            throw new InvalidOperationException("This case is impossible!");
        }
    }

    public static WorkflowDowntime StartsAt(DateTimeOffset startsOnUtc, DateTimeOffset endsOnUtc, TimeProvider? clock = null)
    {
        clock ??= TimeProvider.System;
        var now = clock.GetUtcNow();
        if (startsOnUtc > endsOnUtc) throw new DowntimeMustStartBeforeItEnds();
        if (endsOnUtc < now) throw new DowntimeMustEndInTheFuture();

        return new WorkflowDowntime
        {
            StartsOnUtc = startsOnUtc,
            EndsOnUtc = endsOnUtc,
            CreatedOnUtc = now,
            Checksum = CalculatesChecksum.For(startsOnUtc, endsOnUtc, now),
        };
    }

    public static WorkflowDowntime StartsIn(TimeSpan wait, TimeSpan length, TimeProvider? clock = null)
    {
        clock ??= TimeProvider.System;
        var startsOnUtc = clock.GetUtcNow().Add(wait);
        var endsOnUtc = startsOnUtc.Add(length);

        return StartsAt(startsOnUtc, endsOnUtc);
    }

    public static async Task<WorkflowDowntime?> FromFile(string workflowPath)
    {
        var workflowDowntimePath = Path.ChangeExtension(workflowPath, FileExtension);

        // core: This workflow has no lock.
        if (!Path.Exists(workflowDowntimePath)) return null;

        // core: Avoid race conditions by locking file operations.
        await Lock.WaitAsync();
        try
        {
            await using var fileStream = new FileStream(workflowDowntimePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (await JsonSerializer.DeserializeAsync<WorkflowDowntime>(fileStream) is { } workflowDowntime)
            {
                if (workflowDowntime.Checksum != CalculatesChecksum.For(workflowDowntime.StartsOnUtc, workflowDowntime.EndsOnUtc, workflowDowntime.CreatedOnUtc))
                {
                    throw new WorkflowDowntimeCorrupted(workflowPath);
                }

                return workflowDowntime with { FileName = workflowDowntimePath };
            }

            throw new WorkflowDowntimeNull(workflowDowntimePath);
        }
        finally
        {
            Lock.Release();
        }
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
                WriteIndented = true
            });
            await stream.FlushAsync();
        }
        finally
        {
            Lock.Release();
        }

        return lockPath;
    }

    public async ValueTask EndsNow()
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

public static class CalculatesChecksum
{
    public static string For(params DateTimeOffset[] values)
    {
        // meta: This is a very simple checksum, but it is good enough for our purposes.
        var value = string.Join("_", values.Select(x => x.ToString("O")));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}

public enum WorkflowDowntimeStatus
{
    Pending,
    Ongoing,
    Expired,
}

public abstract class WorkflowDowntimeIssue(string path) : Exception
{
    public string Path => path;
}

public class WorkflowDowntimeNull(string path) : WorkflowDowntimeIssue($"The '{path}' is not a valid workflow-lock-file.");

public class WorkflowDowntimeCorrupted(string path) : WorkflowDowntimeIssue($"The checksum for '{path}' does not match the timestamps.");

public class DowntimeMustStartBeforeItEnds : Exception;

public class DowntimeMustEndInTheFuture : Exception;