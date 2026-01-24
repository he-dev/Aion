using System.Threading.Tasks;
using Aion.Util.Services.Synchronizations;

namespace Aion.Util.Services;

public interface ISynchronizeWorkflow
{
    // https://www.quartz-scheduler.net/documentation/quartz-3.x/quick-start.html

    Task<SynchronizeWorkflowResult?> Try(Workflow workflow);
}


