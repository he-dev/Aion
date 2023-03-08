namespace AionApi.Models;

public class WorkflowEngineOptions
{
    public int UpdaterStartDelay { get; set; } = default!;
    
    public string UpdaterSchedule { get; set; } = default!;

    public string StoreDirectory { get; set; } = default!;
}