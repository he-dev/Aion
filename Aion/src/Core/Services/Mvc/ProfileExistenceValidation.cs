using System;
using System.Linq;
using Aion.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aion.Core.Services.Mvc;

public class ProfileExistenceValidation : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // meta: Get the profileName parameter from the route.
        if (!context.ActionArguments.TryGetValue("profileName", out var profileNameObj) || profileNameObj is not string profileName)
        {
            context.Result = new BadRequestObjectResult("Profile name is required.");
            return;
        }

        // meta: Get the engine options from DI.
        var engineOptions = context.HttpContext.RequestServices.GetRequiredService<IOptionsSnapshot<InstanceOptions>>();

        // core: Check the profile exists.
        if (!engineOptions.Value.Profiles.Any(p => p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase)))
        {
            context.Result = new NotFoundObjectResult($"Profile '{profileName}' not found.");
            return;
        }

        base.OnActionExecuting(context);
    }
}