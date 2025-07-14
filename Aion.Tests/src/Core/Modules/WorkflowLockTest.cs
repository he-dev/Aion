using Aion.Core.Modules;
using Microsoft.Extensions.Time.Testing;

namespace Aion.Tests.Core.Modules;

public class WorkflowLockTest
{
    [Fact]
    public void IsPendingWhenStartBeforeNow()
    {
        // core: The lock starts in an hour from the fake now.
        var fakeNowUtc = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 13, 0, 0, TimeSpan.Zero));
        var fakeStartsOnUtcNow = new DateTimeOffset(2025, 1, 1, 14, 0, 0, TimeSpan.Zero);
        var fakeEndsOnUtcNow = new DateTimeOffset(2025, 1, 1, 15, 0, 0, TimeSpan.Zero);

        var workflowLock = WorkflowLock.StartAt(fakeStartsOnUtcNow, fakeEndsOnUtcNow, fakeNowUtc) with { Clock = fakeNowUtc };

        Assert.True(workflowLock.IsPending);
        Assert.False(workflowLock.IsRunning);
        Assert.False(workflowLock.IsExpired);
        Assert.Equal(TimeSpan.FromHours(2), workflowLock.Remaining);
        Assert.Equal(TimeSpan.FromHours(1), workflowLock.Duration);
    }

    [Fact]
    public void IsRunningWhenNowBetweenStartAndEnd()
    {
        // core: The fake now is between start and end.
        var fakeStartsOnUtcNow = new DateTimeOffset(2025, 1, 1, 13, 0, 0, TimeSpan.Zero);
        var fakeNowUtc = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 14, 0, 0, TimeSpan.Zero));
        var fakeEndsOnUtcNow = new DateTimeOffset(2025, 1, 1, 15, 0, 0, TimeSpan.Zero);

        var workflowLock = WorkflowLock.StartAt(fakeStartsOnUtcNow, fakeEndsOnUtcNow, fakeNowUtc) with { Clock = fakeNowUtc };

        Assert.False(workflowLock.IsPending);
        Assert.True(workflowLock.IsRunning);
        Assert.False(workflowLock.IsExpired);
        Assert.Equal(TimeSpan.FromHours(1), workflowLock.Remaining);
        Assert.Equal(TimeSpan.FromHours(2), workflowLock.Duration);
    }

    [Fact]
    public void IsExpiredWhenEndAfterNow()
    {
        // core: The fake now is after end.
        var fakeStartsOnUtcNow = new DateTimeOffset(2025, 1, 1, 13, 0, 0, TimeSpan.Zero);
        var fakeEndsOnUtcNow = new DateTimeOffset(2025, 1, 1, 14, 0, 0, TimeSpan.Zero);
        var fakeUtcNow = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 13, 30, 0, TimeSpan.Zero));
        var fakeUtcLater = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 15, 0, 0, TimeSpan.Zero));

        var workflowLock = WorkflowLock.StartAt(fakeStartsOnUtcNow, fakeEndsOnUtcNow, fakeUtcNow) with { Clock = fakeUtcLater };

        Assert.False(workflowLock.IsPending);
        Assert.False(workflowLock.IsRunning);
        Assert.True(workflowLock.IsExpired);
        Assert.Equal(TimeSpan.FromHours(-1), workflowLock.Remaining);
        Assert.Equal(TimeSpan.FromHours(1), workflowLock.Duration);
    }

    [Fact]
    public async Task ThrowsWhenDeletingOfNotSavedLock()
    {
        // core: The lock starts in an hour from the fake now.
        var fakeNowUtc = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 13, 0, 0, TimeSpan.Zero));
        var fakeStartsOnUtcNow = new DateTimeOffset(2025, 1, 1, 14, 0, 0, TimeSpan.Zero);
        var fakeEndsOnUtcNow = new DateTimeOffset(2025, 1, 1, 15, 0, 0, TimeSpan.Zero);

        var workflowLock = WorkflowLock.StartAt(fakeStartsOnUtcNow, fakeEndsOnUtcNow, fakeNowUtc) with { Clock = fakeNowUtc };

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await workflowLock.Delete());
    }

    [Fact]
    public async Task CanSaveAndDeleteLock()
    {
        // core: The lock starts in an hour from the fake now.
        var fakeNowUtc = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 13, 0, 0, TimeSpan.Zero));
        var fakeStartsOnUtcNow = new DateTimeOffset(2025, 1, 1, 14, 0, 0, TimeSpan.Zero);
        var fakeEndsOnUtcNow = new DateTimeOffset(2025, 1, 1, 15, 0, 0, TimeSpan.Zero);

        var workflowLock = WorkflowLock.StartAt(fakeStartsOnUtcNow, fakeEndsOnUtcNow, fakeNowUtc) with { Clock = fakeNowUtc };
        var lockPath = await workflowLock.SaveFor(@"workflows\says-hallo.json");

        Assert.True(File.Exists(lockPath));

        workflowLock = await WorkflowLock.FromFile(lockPath);
        workflowLock = workflowLock with { Clock = fakeNowUtc };

        Assert.True(workflowLock.IsPending);
        Assert.False(workflowLock.IsRunning);
        Assert.False(workflowLock.IsExpired);
        Assert.Equal(TimeSpan.FromHours(2), workflowLock.Remaining);
        Assert.Equal(TimeSpan.FromHours(1), workflowLock.Duration);

        await workflowLock.Delete();

        Assert.False(File.Exists(lockPath));;
    }
}