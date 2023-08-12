using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AionApi.Filters;

internal class ValidateArgument : Attribute, IAsyncActionFilter
{
    public ValidateArgument(string name, string[] allowedValues)
    {
        Name = name;
        AllowedValues = allowedValues.ToHashSet(StringComparer.InvariantCultureIgnoreCase);
    }

    private string Name { get; }

    private ISet<string> AllowedValues { get; }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionArguments.TryGetValue(Name, out var value) && !AllowedValues.Contains(value))
        {
            context.Result = new BadRequestObjectResult(new { message = $"Unsupported '{Name}' value: '{value}'.", AllowedValues });
        }
        else
        {
            await next();
        }
    }
}