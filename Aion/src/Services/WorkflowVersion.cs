using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Reusable;
using Reusable.Extensions;

namespace Aion.Services;

internal static class WorkflowVersion
{
    public static string FindLatest(string path)
    {
        var match = Regex.Match(path, "(?<before>.+){version}(?<after>.+)");
        if (match.Success)
        {
            var latestVersion =
                Directory
                    .GetDirectories(match.Groups["before"].Value)
                    .Select(dir => SemanticVersion.TryParse(Path.GetFileName(dir), out var ver) ? ver : SemanticVersion.Zero)
                    .Where(ver => ver > SemanticVersion.Zero)
                    .OrderByDescending(ver => ver)
                    .FirstOrDefault();

            if (latestVersion is { })
            {
                return Path.Combine(match.Groups["before"].Value.Trim('\\'), latestVersion.Also(ver => { ver.Prefix = true; }), match.Groups["after"].Value.Trim('\\'));
            }
        }

        return path;
    }
}