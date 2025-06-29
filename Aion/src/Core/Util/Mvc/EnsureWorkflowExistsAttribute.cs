using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Util.Mvc;

public class EnsureWorkflowExistsAttribute
(
    ILogger<EnsureWorkflowExistsAttribute> logger,
    WorkflowDirectory workflowDirectory
) : ActionFilterAttribute
{
    public new int Order => 1;

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // ?? We can ignore the null as this will be 100% set at this point as
        // otherwise the route wouldn't even have matched.
        var name = context.ActionArguments["name"] as string;
        if (await workflowDirectory.Find(name!) is { } workflow)
        {
            // Find the first parameter of type Workflow and set it
            var workflowParam =
                context
                    .ActionDescriptor
                    .Parameters
                    .FirstOrDefault(p => p.ParameterType == typeof(Workflow));

            if (workflowParam is not null)
            {
                // ?? Inject workflow into action arguments.
                context.ActionArguments[workflowParam.Name] = workflow;
                await next();
            }
            else
            {
                var controllerName = context.ActionDescriptor.RouteValues["controller"];
                var actionName = context.ActionDescriptor.RouteValues["action"];
                //throw new InvalidOperationException($"Action '{controllerName}.{actionName}' requires a parameter of type {nameof(Workflow)}.");
            }
        }
        else
        {
            logger.LogDebug("Workflow '{name}' not found.", name);
            context.Result = new NotFoundObjectResult(new { name });
        }
    }
}