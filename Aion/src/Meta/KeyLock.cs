using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Aion.Meta;

public static class KeyLock
{
    private static readonly ConcurrentDictionary<object, SemaphoreSlim> Locks = new();
    private static readonly ConcurrentDictionary<object, int> Counters = new();
    private static readonly object Lock = new();

    public static async Task<IDisposable> AcquireAsync(object key, CancellationToken cancellationToken = default)
    {
        var semaphore = default(SemaphoreSlim);
        lock (Lock)
        {
            semaphore = Locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            Counters.AddOrUpdate(key, 1, (_, count) => count + 1);
        }

        await semaphore.WaitAsync(cancellationToken);
        return new Release(semaphore, key);
    }

    private class Release(SemaphoreSlim semaphore, object key) : IDisposable
    {
        public void Dispose()
        {
            semaphore.Release();
            lock (Lock)
            {
                if (Counters.AddOrUpdate(key, -1, (_, count) => count - 1) == 0)
                {
                    Locks.TryRemove(key, out _);
                    Counters.TryRemove(key, out _);
                    // ?? Not necessary to dispose because it does not use the AvailableWaitHandle property.
                    // .. see https://stackoverflow.com/a/39195920/235671
                }
            }
        }
    }
}