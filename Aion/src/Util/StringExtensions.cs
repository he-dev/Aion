using System;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Aion.Util;

public static class StringExtensions
{
    public static bool IsLike(this string text, string? pattern, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        var matcher = new Matcher(comparison);
        matcher.AddInclude(pattern ?? "*");
        return matcher.Match(text).HasMatches;
    }
}