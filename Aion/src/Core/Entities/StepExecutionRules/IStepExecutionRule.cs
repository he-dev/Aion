using System.Collections.Immutable;

namespace Aion.Core.Entities.StepExecutionRules;

public interface IStepExecutionRule
{
    bool Violated(Workflow.Step step, IImmutableList<int?> exitCodes);
}