using AionApi.Services;
using AionApi.Utilities;

namespace AionApi.Tests;

public class LatestVersionTest
{
    private static readonly IEnumerable<string> Directories = new[]
    {
        "baz-v1.0.0",
        "baz-v1",
        "baz-v1.5",
        "baz-v3.8.4",
        "baz-v1.4.2",
        "baz-v1.4.4",
        "baz-v1.4.3",
    };

    [Theory]
    [InlineData(@"c:\foo\bar\baz-v{version:latest}\baz.exe", @"c:\foo\bar\baz-v3.8.4\baz.exe")]
    [InlineData(@"c:\foo\bar\baz-v{version:1.4}\baz.exe", @"c:\foo\bar\baz-v1.4.4\baz.exe")]
    [InlineData(@"c:\foo\bar\baz-v{version:1}\baz.exe", @"c:\foo\bar\baz-v1.5\baz.exe")]
    [InlineData(@"c:\foo\bar\baz-v{version:1.4.2}\baz.exe", @"c:\foo\bar\baz-v1.4.2\baz.exe")]
    [InlineData(@"c:\foo\bar\baz-v{version:1.6.2}\baz.exe", null)]
    public void MatchesVersion(string path, string expected)
    {
        Assert.Equal(expected, LatestVersion.Find(path, _ => Directories));
    }
}