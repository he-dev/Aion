using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Aion.Core.Modules;

public record WorkflowLock : IAsyncDisposable
{
    private static readonly SemaphoreSlim Lock = new(1, 1);

    public DateTimeOffset StartsOnUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset EndsOnUtc { get; init; }
    public DateTimeOffset CreatedOnUtc { get; init; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public string FileName { get; init; } = null!;

    public TimeSpan Length => EndsOnUtc - StartsOnUtc;
    public TimeSpan Remaining => EndsOnUtc - DateTimeOffset.UtcNow;

    public bool IsPending => StartsOnUtc < DateTimeOffset.UtcNow;
    public bool IsExpired => EndsOnUtc < DateTimeOffset.UtcNow;

    public static WorkflowLock Create(DateTimeOffset startsOnUtc, DateTimeOffset endsOnUtc)
    {
        if (startsOnUtc > endsOnUtc)
        {
            throw new ArgumentException("Workflow lock's start must be before end.", nameof(startsOnUtc));
        }

        if (endsOnUtc < DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("Workflow lock's expiry must be in the future.", nameof(endsOnUtc));
        }

        return new WorkflowLock
        {
            StartsOnUtc = startsOnUtc,
            EndsOnUtc = endsOnUtc,
        };
    }

    public static async Task<WorkflowLock?> FromFile(string workflowPath)
    {
        var workflowLockPath = Path.ChangeExtension(workflowPath, FileExtension.Lock);

        if (!Path.Exists(workflowLockPath))
        {
            return null;
        }

        await Lock.WaitAsync();
        try
        {
            await using var fileStream = new FileStream(workflowLockPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (await JsonSerializer.DeserializeAsync<WorkflowLock>(fileStream) is { } workflowLock)
            {
                return workflowLock with { FileName = workflowLockPath };
            }
        }
        finally
        {
            Lock.Release();
        }

        return null;
    }

    public async Task<string> SaveFor(string workflowPath)
    {
        var lockPath = Path.ChangeExtension(workflowPath, FileExtension.Lock);

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

    public async ValueTask DisposeAsync()
    {
        if (IsExpired)
        {
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