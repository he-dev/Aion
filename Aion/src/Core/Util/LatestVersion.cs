using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Reusable;

namespace AionApi.Utilities;

public delegate IEnumerable<string> EnumerateDirectoriesFunc(string path);

public static class LatestVersion
{
    public static string? Find(string fileName, EnumerateDirectoriesFunc directories)
    {
        var names = fileName.Split('\\');
        var path = new List<string>();

        // language=regex
        const string versionPattern = @"(?:\.?(?<numeric>0|[1-9]\d*)){1,3}";

        foreach (var name in names)
        {
            // https://regex101.com/r/jedbZR/1
            // https://regex101.com/r/F0bKCy/1
            // https://regex101.com/r/YUT36C/1
            if (Regex.Match(name, @"\{version:(?:(?<latest>latest)|(?:\.?(?<numeric>0|[1-9]\d*)){1,3})\}") is { Success: true } match)
            {
                var prefix = name[..match.Index];
                var suffix = match.Index + match.Length is var startAt && startAt > name.Length ? name[(match.Index + match.Length)..^1] : string.Empty;

                var versionRegex = new Regex(Regex.Escape(prefix) + versionPattern + Regex.Escape(suffix));

                var candidates =
                    from directory in directories(Path.Join(path.ToArray()))
                    let versions = versionRegex.Match(directory)
                    where versions.Success
                    let values = versions.Groups["numeric"].Captures.Select(x => int.Parse(x.Value))
                    let version = new SemanticVersion(values.ElementAtOrDefault(0), values.ElementAtOrDefault(1), values.ElementAtOrDefault(2))
                    orderby version descending
                    select new { directory, version };


                if (match.Groups["latest"] is { Success: true })
                {
                    var latest =
                        candidates
                            .Select(x => x.directory)
                            .FirstOrDefault();
                    if (latest is { })
                    {
                        path.Add(latest);
                        continue;
                    }

                    return default;
                }

                if (match.Groups["numeric"] is { Success: true } numeric)
                {
                    var major = int.Parse(numeric.Captures[0].Value);
                    var minor = numeric.Captures.Count > 1 ? int.Parse(numeric.Captures[1].Value) : new int?();
                    var patch = numeric.Captures.Count > 2 ? int.Parse(numeric.Captures[2].Value) : new int?();

                    var latest =
                        candidates
                            .Where(x =>
                                (x.version.Major == major) &&
                                (!minor.HasValue || x.version.Minor == minor.Value) &&
                                (!patch.HasValue || x.version.Patch == patch.Value))
                            .Select(x => x.directory)
                            .FirstOrDefault();

                    if (latest is not null)
                    {
                        path.Add(latest);
                        continue;
                    }

                    return default;
                }
            }
            else
            {
                path.Add(name);
            }
        }

        return Path.Join(path.ToArray());
    }
}