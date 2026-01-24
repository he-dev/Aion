using System.Collections.Generic;
using System.Linq;

namespace Aion.Util.Templates;

public class CompositeVariableGroup(IGrouping<string, TemplateVariableGroup> grouping) : TemplateVariableGroup(grouping.Key)
{
    public override IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        return
            grouping
                // core: Flatten nested groups.
                .SelectMany(group => group)
                // core: The last property wins.
                .GroupBy(x => x.Key, (_, items) => items.Last())
                .GetEnumerator();
    }
}