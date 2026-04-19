namespace Aion.Util;

public record SynchronizationStep(string Name, object? Result = null)
{
    public record ScheduleJob(object? Result) : SynchronizationStep(nameof(ScheduleJob), Result);
    public record DeleteJob(object? Result) : SynchronizationStep(nameof(DeleteJob), Result);
    public record RescheduleJob(object? Result) : SynchronizationStep(nameof(RescheduleJob), Result);
}

