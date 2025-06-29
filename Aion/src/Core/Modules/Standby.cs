using System;
using System.IO;
using System.Threading.Tasks;
using Aion.Util;
using Aion.Util.Yaml;

namespace Aion.Core.Modules;

public record Standby
{
    [YamlDotNet.Serialization.YamlIgnore]
    public string Path { get; init; } = null!;

    public string Filter { get; init; } = null!;

    public DateTimeOffset StartsOnUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset EndsOnUtc { get; init; }
    public DateTimeOffset CreatedOnUtc { get; init; } = DateTimeOffset.UtcNow;

    [YamlDotNet.Serialization.YamlIgnore]
    public TimeSpan Length => EndsOnUtc - StartsOnUtc;

    [YamlDotNet.Serialization.YamlIgnore]
    public TimeSpan Remaining => EndsOnUtc - DateTimeOffset.UtcNow;

    [YamlDotNet.Serialization.YamlIgnore]
    public bool IsActive => StartsOnUtc <= DateTimeOffset.UtcNow && EndsOnUtc > DateTimeOffset.UtcNow;

    [YamlDotNet.Serialization.YamlIgnore]
    public bool IsExpired => EndsOnUtc < DateTimeOffset.UtcNow;

    public bool Matches(string value)
    {
        var matcher = new WorkflowMatcher(Filter);
        return matcher.Matches(value);
    }

    public static Standby Schedule(string filter, DateTimeOffset startsOnUtc, DateTimeOffset endsOnUtc)
    {
        if (startsOnUtc > endsOnUtc)
        {
            throw new ArgumentException("Standby token start must be before end.", nameof(startsOnUtc));
        }

        if (endsOnUtc < DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("Standby token expiry must be in the future.", nameof(endsOnUtc));
        }

        return new Standby
        {
            Filter = filter,
            StartsOnUtc = startsOnUtc,
            EndsOnUtc = endsOnUtc,
        };
    }

    public static async Task<Standby> FromFile(string path)
    {
        using var _ = await KeyLock.AcquireAsync(path);

        var yamlDeserializer =
            new YamlDotNet.Serialization.DeserializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                .Build();

        await using var yamlFileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var yamlStreamReader = new StreamReader(yamlFileStream);
        return yamlDeserializer.Deserialize<Standby>(yamlStreamReader) with { Path = path };
    }

    public async Task SaveTo(string path)
    {
        var startsOnStr = StartsOnUtc.ToString("yyyyMMdd_HHmm", System.Globalization.CultureInfo.InvariantCulture);
        var expiresOnStr = EndsOnUtc.ToString("yyyyMMdd_HHmm", System.Globalization.CultureInfo.InvariantCulture);
        var fileName = $"standby_from_{startsOnStr}_to_{expiresOnStr}.yaml";

        path = System.IO.Path.Join(path, fileName);

        var serializer =
            new YamlDotNet.Serialization.SerializerBuilder()
                .WithTypeConverter(new YamlDateTimeOffsetConverter())
                .Build();

        await using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
        serializer.Serialize(writer, this);
        await writer.FlushAsync();
    }

    public async Task Delete()
    {
        using var _ = await KeyLock.AcquireAsync(Path);
        File.Delete(Path);
    }
}