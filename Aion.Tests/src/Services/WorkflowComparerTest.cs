using System.Linq;
using Aion.Data;
using Aion.Services;
using Xunit;

namespace Aion.Tests.Services;

using static WorkflowAction;

public class WorkflowComparerTest
{
    [Fact]
    public void CanDetectChanges()
    {
        // Registered.
        var x = new[]
        {
            new Workflow { Name = "unchanged", Schedule = "foo", Enabled = true },

            // Remove
            new Workflow { Name = "remove missing", Schedule = "foo", Enabled = true },
            new Workflow { Name = "remove disabled", Schedule = "foo", Enabled = true },

            // Update
            new Workflow { Name = "update changed", Schedule = "foo", Enabled = true },
        };

        // Other.
        var y = new[]
        {
            new Workflow { Name = "unchanged", Schedule = "foo", Enabled = true },

            // Remove
            // new Workflow { Name = "remove missing", Schedule = "foo", Enabled = true},
            new Workflow { Name = "remove disabled", Schedule = "foo", Enabled = false },

            // Update
            new Workflow { Name = "update changed", Schedule = "bar", Enabled = true },

            // Create
            new Workflow { Name = "create new", Schedule = "foo", Enabled = true },
            new Workflow { Name = "don't create disabled", Schedule = "foo", Enabled = false },
        };

        var deltas = x.Compare(y).ToDictionary(d => d.Workflow.Name);

        Assert.False(deltas.ContainsKey("unchanged"));

        Assert.Equal(Remove, deltas["remove missing"].Action);
        Assert.Equal(Remove, deltas["remove disabled"].Action);

        Assert.Equal(Update, deltas["update changed"].Action);

        Assert.Equal(Create, deltas["create new"].Action);
        Assert.False(deltas.ContainsKey("don't create disabled"));
    }
}