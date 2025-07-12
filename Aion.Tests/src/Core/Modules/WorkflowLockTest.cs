using Aion.Core.Modules;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aion.Tests.Core.Modules;

public class WorkflowLockTest
{
    [Fact]
    public void WorkflowLock_IsRunning_ReturnsTrue_WhenTimeIsWithinLockPeriod()
    {
        // Arrange: Create a fake clock set to a specific time.
        var fakeUtcNow = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var fakeUtcNowForStart = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero));

        // Act: Create a lock that starts now and lasts for 1 hour.
        // We pass the fake clock to the factory method.
        var workflowLock = WorkflowLock.StartIn(TimeSpan.Zero, TimeSpan.FromHours(1), fakeUtcNowForStart) with
        {
            Clock = fakeUtcNow
        };

        // Assert: At the moment of creation, the lock should be running.
        Assert.True(workflowLock.IsRunning);

        // Arrange: Advance the fake clock by 30 minutes.
        //fakeTimeProvider.Advance(TimeSpan.FromMinutes(30));

        // Assert: The lock should still be running.
        Assert.True(workflowLock.IsRunning);
        WebApplicationFactory>
        // Arrange: Advance the clock to the exact expiry time.
        //fakeTimeProvider.Advance(TimeSpan.FromMinutes(30));

        // Assert: The lock is now expired and no longer running.
        // (Because IsRunning uses a > comparison on the end time).
        Assert.False(workflowLock.IsRunning);
        Assert.True(workflowLock.IsExpired);
    }
}