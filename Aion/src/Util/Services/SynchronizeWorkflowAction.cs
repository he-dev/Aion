using System.Collections.Generic;

namespace Aion.Util.Services;

public interface SynchronizeWorkflowAction
{
    IAsyncEnumerable<SynchronizationStep> Invoke(Workflow workflow);
}


