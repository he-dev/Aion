namespace AionApi.Models;

public class WorkflowEngineOptions
{
    public bool UpdaterEnabled { get; set; }
    
    public int UpdaterStartDelay { get; set; } = default!;
    
    public string UpdaterSchedule { get; set; } = default!;

    public string WorkflowDirectory { get; set; } = default!;
}