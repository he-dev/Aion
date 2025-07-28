using System.Collections.Immutable;

namespace Aion.Core.StepExecutionRules;

public interface IStepExecutionRule
{
    bool Violated(Workflow.Step step, IImmutableList<int?> exitCodes);
}