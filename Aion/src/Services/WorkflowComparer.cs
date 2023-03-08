using System.Collections.Generic;
using System.Linq;
using Aion.Data;
using Reusable;

namespace Aion.Services;

using static WorkflowAction;

public enum WorkflowAction
{
    None,
    Remove,
    Update,
    Create
}

public record Delta(Workflow Workflow, WorkflowAction Action);

public interface IWorkflow
{
    string Name { get; }
    
    //string C
}

public static class WorkflowComparer
{
    public static IEnumerable<Delta> Compare(this IEnumerable<Workflow> registrations, IEnumerable<Workflow> other)
    {
        var x = registrations.ToDictionary(w => w.Name);
        var y = other.ToDictionary(w => w.Name);

        // New workflows must be enabled and not already scheduled.
        var create =
            from w in y
            where w.Value.Enabled && x.ContainsKey(w.Key) == false
            select new Delta(w.Value, Create);
        
        var removeMissing =
            from w in x
            where y.ContainsKey(w.Key) == false
            select new Delta(w.Value, Remove);
        
        var removeDisabled =
            from r in x
            join o in y on new { r.Key } equals new { o.Key }
            where o.Value.Enabled == false
            select new Delta(r.Value, Remove);
        
        // Changed workflows must be enabled and their schedule must be different.
        var update =
            from r in x
            join o in y on new { r.Key } equals new { o.Key }
            where o.Value.Enabled == true
            where SoftString.Comparer.Equals(r.Value.Schedule, o.Value.Schedule) == false
            select new Delta(o.Value, Update);

        return 
            Enumerable
                .Empty<Delta>()
                .Concat(removeMissing)
                .Concat(removeDisabled)
                .Concat(create)
                .Concat(update);
    }
}