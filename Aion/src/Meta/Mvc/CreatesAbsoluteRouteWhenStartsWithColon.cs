using System.Linq;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Aion.Meta.Mvc;

public class CreatesAbsoluteRouteWhenStartsWithColon : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            // meta: Find all route templates defined on the controller
            var controllerRouteTemplates =
                controller
                    .Selectors
                    .Select(s => s.AttributeRouteModel?.Template)
                    .Where(t => t is not null)
                    .ToList();

            if (!controllerRouteTemplates.Any())
            {
                // note: This actually should be an error...
            }

            foreach (var action in controller.Actions)
            {
                foreach (var selector in action.Selectors)
                {
                    if (selector.AttributeRouteModel is { Template: { } actionRouteTemplate } && actionRouteTemplate.StartsWith(":"))
                    {
                        // core: Create an absolute route to override automatic joining.
                        var combinedTemplate = "/" + controllerRouteTemplates.First() + actionRouteTemplate;

                        // core: Overwrite the action's original route.
                        selector.AttributeRouteModel = new AttributeRouteModel
                        {
                            Template = combinedTemplate,
                            Name = selector.AttributeRouteModel.Name,
                            Order = selector.AttributeRouteModel.Order,
                        };
                    }
                }
            }
        }
    }
}