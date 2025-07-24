using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aion.Util.Scriban;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Core.Providers;

public class ProfileProvider
(
    ILogger<ProfileProvider> logger,
    IOptions<ProfileProviderOptions> options
)
{
    public async Task<JsonObject> FindLoggerProfile(string fileName, string profileName)
    {
        try
        {
            var profile = await FromJson(Path.Combine(options.Value.Path, "logger-profiles.json"));
        }
        catch (Exception ex)
        {
            throw;
        }

        return new JsonObject();
    }

    public static async Task<Profile?> FromJson(string path)
    {
        await using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<Profile>(fileStream, new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip
        });
    }
}

public class ProfileProviderOptions
{
    public string Path { get; set; } = null!;

    public class RenderPath : IPostConfigureOptions<ProfileProviderOptions>
    {
        public void PostConfigure(string? name, ProfileProviderOptions options)
        {
            options.Path = VariableTemplate.Render(options.Path, []);
        }
    }
}

public record Profile
{
    public int Version { get; init; }

    public Dictionary<string, JsonObject> Profiles { get; init; } = null!;
}