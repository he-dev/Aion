using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Util;

public class WorkflowMatcher
{
    private readonly Matcher _matcher = new(StringComparison.OrdinalIgnoreCase);

    public WorkflowMatcher(string? filter)
    {
        _matcher.AddInclude(filter ?? "*");
    }

    public bool Matches(string value) => _matcher.Match(value).HasMatches;
}

public class WorkflowMatcherAttribute
(
    ILogger<WorkflowMatcherAttribute> logger
) : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Find the first parameter of type Workflow and set it
        var matcherParam =
            context
                .ActionDescriptor
                .Parameters
                .FirstOrDefault(p => p.ParameterType == typeof(WorkflowMatcher));

        if (matcherParam is not null)
        {
            // ?? Inject workflow-matcher into action arguments.
            var filter = context.ActionArguments["filter"] as string;
            context.ActionArguments[matcherParam.Name] = new WorkflowMatcher(filter);
            await next();
        }
        else
        {
            var controllerName = context.ActionDescriptor.RouteValues["controller"];
            var actionName = context.ActionDescriptor.RouteValues["action"];
            throw new InvalidOperationException($"Action '{controllerName}.{actionName}' requires a parameter of type {nameof(WorkflowMatcher)}.");
        }
    }
}