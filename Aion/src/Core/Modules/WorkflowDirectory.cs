using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Options;

namespace Aion.Core.Modules;

public class WorkflowDirectory(IOptions<WorkflowEngineOptions> options)
{
    public IEnumerable<string> FindFiles(string fileNameFilter, FileExtension extension)
    {
        if (string.IsNullOrEmpty(fileNameFilter)) throw new ArgumentException("Value cannot be null or empty.", nameof(fileNameFilter));

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude($"**\\{fileNameFilter}.{extension}");

        return
            from path in matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(options.Value.WorkflowDirectory))).Files
            select Path.Join(options.Value.WorkflowDirectory, path.Path);
    }

    public string? FindFile(string fileNameFilter, FileExtension extension)
    {
        using var enumerator = FindFiles(fileNameFilter, extension).GetEnumerator();

        if (!enumerator.MoveNext())
        {
            return null; // No match.
        }

        var first = enumerator.Current;

        if (enumerator.MoveNext())
        {
            // Multiple matches.
            throw new AmbiguousFilterException(fileNameFilter, first, enumerator.Current);
        }

        return first;
    }
}

public record FileExtension(string Name)
{
    public static readonly FileExtension Json = new("json");
    public static readonly FileExtension Lock = new("lock");

    public override string ToString() => Name;

    public static implicit operator string(FileExtension extension) => extension.ToString();
}

public record FileFilter(string Value)
{
    public static readonly FileFilter Any = new("*");

    public static implicit operator string(FileFilter filter) => filter.Value;
}

public class AmbiguousFilterException(string fileNameFilter, params string[] fileNames) : Exception($"Multiple workflows match filter '{fileNameFilter}': {string.Join(',', fileNames)}.");

public class WorkflowNotFoundException(string filter) : Exception($"Filter '{filter}' does not match any workflows.");