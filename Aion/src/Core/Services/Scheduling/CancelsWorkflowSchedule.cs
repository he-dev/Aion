using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Services.Scheduling;

public class CancelsWorkflowSchedule
(
    ILogger<CancelsWorkflowSchedule> logger,
    ISchedulerFactory schedulerFactory
)
{
    public async Task<bool> Where(JobKey jobKey)
    {
        var scheduler = await schedulerFactory.GetScheduler();
        return await scheduler.DeleteJob(jobKey);
    }
}