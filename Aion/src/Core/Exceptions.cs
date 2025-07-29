using System;

namespace Aion.Core;



public class WorkflowNullException(string path) : Exception($"Workflow '{path}' is null.");

public class WorkflowNotFoundException(string filter) : Exception($"Filter '{filter}' does not match any workflows.");

public class MultipleWorkflowsFoundException(string filter) : Exception($"Filter '{filter}' matches multiple workflows.");