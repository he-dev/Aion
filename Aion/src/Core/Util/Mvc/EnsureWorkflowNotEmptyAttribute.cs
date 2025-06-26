using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Util.Mvc;

public class EnsureWorkflowNotEmptyAttribute
(
    ILogger<EnsureWorkflowNotEmptyAttribute> logger
) : ActionFilterAttribute
{
    public new int Order => 2;

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var workflowParam =
            context
                .ActionDescriptor
                .Parameters
                .FirstOrDefault(p => p.ParameterType == typeof(Workflow));

        if (workflowParam is null)
        {
            var controllerName = context.ActionDescriptor.RouteValues["controller"];
            var actionName = context.ActionDescriptor.RouteValues["action"];
            throw new InvalidOperationException($"Action '{controllerName}.{actionName}' requires a parameter of type {nameof(Workflow)}.");
        }

        if (context.ActionArguments[workflowParam.Name] is Workflow workflow)
        {
            if (!workflow.Steps.Any(s => s.Enabled))
            {
                logger.LogWarning("Workflow '{Name}' has no enabled steps or is empty.", workflow.Name);
                context.Result = new UnprocessableEntityObjectResult(new { workflow.Name, message = "Workflow has no enabled steps or is empty." });
                return;
            }
        }
        else
        {
            var controllerName = context.ActionDescriptor.RouteValues["controller"];
            var actionName = context.ActionDescriptor.RouteValues["action"];
            throw new InvalidOperationException($"Action '{controllerName}.{actionName}' workflow parameter '{workflowParam.Name}' is null or not a Workflow.");
        }

        await next();
    }
}