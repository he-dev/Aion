using System;

namespace Aion.Core;

public class ProfileNotFoundException(string profileName) : Exception($"Profile '{profileName}' not found.");

public class WorkflowNullException(string path) : Exception($"Workflow '{path}' is null.");

public class NoMatchException(string profileName, string workflowNameOrFilter)
    : Exception($"No workflows in '{profileName}' matches '{workflowNameOrFilter}'.");

public class AmbiguousMatchException(string profileName, string workflowNameOrFilter)
    : Exception($"More than one workflow in '{profileName}' matches '{workflowNameOrFilter}'.");