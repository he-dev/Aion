using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AionApi.Models;
using Microsoft.Extensions.Options;
using Reusable.IO;
using Reusable.IO.Abstractions;

namespace AionApi.Services;

public class WorkflowDirectory : IEnumerable<string>
{
    public WorkflowDirectory(IOptions<WorkflowEngineOptions> options, IDirectoryTree directoryTree)
    {
        DirectoryTree = directoryTree;
        Name = Environment.ExpandEnvironmentVariables(options.Value.WorkflowDirectory);
    }
    
    private IDirectoryTree DirectoryTree { get; }

    private string Name { get; }

    public override string ToString() => Name;

    public IEnumerator<string> GetEnumerator()
    {
        var fileNames =
            from branch in DirectoryTree.Walk(this)
            from item in branch.Files()
            where item.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            select item.Relative;

        return fileNames.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static implicit operator string(WorkflowDirectory workflowDirectory) => workflowDirectory.Name;

    public static string operator +(WorkflowDirectory workflowDirectory, string fileName) => Path.Combine(workflowDirectory, fileName);
}