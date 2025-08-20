using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Quartz;

namespace Aion.Core.Entities;

public static class WorkflowTemplateValidation
{
    public static WorkflowTemplate Validate(WorkflowTemplate template)
    {
        var results = new List<ValidationResult>();
        Validation.TryValidate(template, results);

        foreach (var item in template.Steps.Select((step, index) => (step, index)).Where(x => x.step.Enabled))
        {
            Validation.TryValidate(item.step, results, new Dictionary<object, object?>
            {
                ["Parent"] = $"{nameof(WorkflowTemplate.StepTemplate)}[{item.index}]"
            });
        }

        return
            results.Any()
                ? throw new ValidationException(string.Join(" | ", results.ConvertAll(r => r.ErrorMessage)))
                : template;
    }
}

public static class Validation
{
    public static bool TryValidate(object obj, ICollection<ValidationResult> results, IDictionary<object, object?>? items = null)
    {
        var context = new ValidationContext(obj, items: items);
        return Validator.TryValidateObject(context.ObjectInstance, context, results, validateAllProperties: true);
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public abstract class StringValidationAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
    {
        var parent = ctx.Items.TryGetValue("Parent", out var p) && p is string s ? s : ctx.ObjectInstance.GetType().Name;
        var propertyType = ctx.ObjectType.GetProperty(ctx.MemberName!)?.PropertyType;
        if (propertyType != typeof(string))
        {
            return new ValidationResult($"{parent}.{ctx.MemberName} cannot be validated with {GetType().Name} as it applies only to string properties.");
        }

        return ValidateString(value as string, parent, ctx);
    }

    protected abstract ValidationResult? ValidateString(string? value, string parent, ValidationContext ctx);
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotNullOrWhiteSpaceAttribute : StringValidationAttribute
{
    protected override ValidationResult? ValidateString(string? value, string parent, ValidationContext ctx)
    {
        return
            string.IsNullOrWhiteSpace(value)
                ? new ValidationResult($"{parent}.{ctx.MemberName} must not be null or white-space.")
                : ValidationResult.Success;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CronAttribute : StringValidationAttribute
{
    protected override ValidationResult? ValidateString(string? value, string parent, ValidationContext ctx)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new ValidationResult($"{parent}.{ctx.MemberName} must not be null or white-space.");
        }

        try
        {
            CronExpression.ValidateExpression(value);
        }
        catch (FormatException ex)
        {
            return new ValidationResult($"{parent}.{ctx.MemberName} is not a valid cron expression: {ex.Message}.", [ctx.MemberName!]);
        }

        return ValidationResult.Success;
    }
}