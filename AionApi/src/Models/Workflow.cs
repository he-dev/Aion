using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AionApi.Models;

[PublicAPI]
public class Workflow
{
    public bool Enabled { get; set; } = true;

    public string Name { get; set; } = default!;

    public string Schedule { get; set; } = default!;

    public Dictionary<string, object> Variables { get; set; } = new();

    public List<Command> Commands { get; set; } = new();

    [PublicAPI]
    public class Command
    {
        public string FileName { get; set; } = default!;

        public IEnumerable<string> Arguments { get; set; } = Enumerable.Empty<string>();

        public string WorkingDirectory { get; set; } = string.Empty;

        public int TimeoutMilliseconds { get; set; } = -1;

        public bool Enabled { get; set; } = true;

        public bool DependsOnPrevious { get; set; } = false;

        public static implicit operator bool(Command command) => command.Enabled;
    }

    public static implicit operator bool(Workflow workflow) => workflow.Enabled;
}

public class WorkflowBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        //var workflow = await JsonSerializer.DeserializeAsync<Workflow>(bindingContext.HttpContext.Request.BodyReader.AsStream());
        //workflow.Name = bindingContext.HttpContext.Request.Path;
        //bindingContext.Result = ModelBindingResult.Success(workflow);
        return Task.CompletedTask;
    }
}