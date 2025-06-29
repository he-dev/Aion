using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Aion.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Core.Modules;

public class StandbyDirectory
(
    ILogger<StandbyDirectory> logger,
    IOptions<StandbyEngineOptions> standbyOptions
) : IAsyncEnumerable<Standby>
{
    public async IAsyncEnumerator<Standby> GetAsyncEnumerator(CancellationToken cancellationToken = new())
    {
        foreach (var path in new DirectoryTree(standbyOptions.Value.PendingPath).First().Files())
        {
            yield return await Standby.FromFile(path);
        }
    }
}