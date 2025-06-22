using System;
using System.Collections.Immutable;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Aion.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Utilities;

public record MaintenanceTokenOptions
{
    public string PendingPath { get; init; } = null!;

    public string? ExpiredPath { get; init; }
};

public class MaintenanceToken(ILogger<MaintenanceToken> logger, IOptions<MaintenanceTokenOptions> options)
{
    private SemaphoreSlim Gate { get; } = new(initialCount: 1, maxCount: 1);

    private DirectoryTree MaintenanceDirectory { get; } = new(VariableTemplate.Render(options.Value.PendingPath, []));

    /// <summary>
    /// Sets or clears the pause expiry for a job.
    /// </summary>
    /// <param name="filter">The unique name of the job.</param>
    /// <param name="expiresOnUtc">The UTC timestamp when the pause expires, or null to unpause the job.</param>
    /// <returns>True if the operation was successful, false otherwise.</returns>
    public async Task<Data> Create(string? filter, DateTimeOffset expiresOnUtc)
    {
        var token = new Data
        {
            Filter = filter,
            ExpiresOnUtc = expiresOnUtc,
        };

        if (token.IsExpired)
        {
            throw new ArgumentException("Maintenance token expiry must be in the future.", nameof(expiresOnUtc));
        }

        try
        {
            Directory.CreateDirectory(MaintenanceDirectory.Path);

            var expiresOn = token.ExpiresOnUtc.ToString("yyyyMMdd_HHmm", System.Globalization.CultureInfo.InvariantCulture);
            var fileName = $"maintenance_until_{expiresOn}.json";
            var filePath = Path.Join(MaintenanceDirectory.Path, fileName);

            // .. Atomic-write: first use the temp-file, then replace the original.
            // ?? Use a block-using so that the file-stream is released for the move. Otherwise, it remains locked and fails.
            await using (var fileStream = new FileStream(filePath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(fileStream, token, new JsonSerializerOptions { WriteIndented = true });
            }

            File.Move(filePath + ".tmp", filePath, overwrite: true);

            logger.LogInformation("Maintenance token '{name}' expires in {expiresIn:N1} minutes.", filter, token.Remaining.TotalMinutes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating maintenance token '{name}'.", filter);
            throw;
        }

        return token;
    }

    /// <summary>
    /// Gets the pause expiry timestamp for a specific job.
    /// </summary>
    /// <param name="name">The unique name of the job.</param>
    /// <returns>The DateTimeOffset (UTC) when the job is paused until, or null if not paused or an error occurs.</returns>
    public async Task<IImmutableList<Data>> Pending()
    {
        // !! Let only a single caller at a time search for tokens.
        await Gate.WaitAsync();

        var expiredPath =
            options.Value.ExpiredPath is not null
                ? VariableTemplate.Render(options.Value.ExpiredPath, [])
                : null;

        var pendingTokens = ImmutableList<Data>.Empty;

        try
        {
            // .. Check all tokens in that directory.
            foreach (var filePath in MaintenanceDirectory.First().Files())
            {
                if (!File.Exists(filePath))
                {
                    // .. The file was deleted while we were iterating.
                    continue;
                }

                try
                {
                    if (await ReadToken(filePath) is { } token)
                    {
                        if (token.IsExpired)
                        {
                            // !! Deal with expired tokens by either archiving or deleting them.

                            if (expiredPath is not null)
                            {
                                Directory.CreateDirectory(expiredPath);
                                File.Move(filePath, Path.Join(expiredPath, Path.GetFileName(filePath)));
                                logger.LogDebug("Expired maintenance token '{name}' archived.", token.Filter);
                            }
                            else
                            {
                                File.Delete(filePath);
                                logger.LogDebug("Expired maintenance token '{name}' deleted.", token.Filter);
                            }
                        }
                        else
                        {
                            // .. Keep pending tokens.
                            logger.LogDebug("Pending maintenance token '{name}' expires in {remaining}.", token.Filter, token.Remaining);
                            pendingTokens = pendingTokens.Add(token);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error reading maintenance token '{name}'.", filePath);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading maintenance tokens.");
        }
        finally
        {
            Gate.Release();
        }

        logger.LogDebug("Found {count} pending maintenance tokens.", pendingTokens.Count);
        return pendingTokens.ToImmutableList();
    }

    // !! Release file handles so it can be moved when expired.
    // ?? With this helper method we can avoid spaghetti helper variables elsewhere.
    private static async Task<Data?> ReadToken(string filePath)
    {
        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<Data>(fileStream);
    }

    public record Data
    {
        public string? Filter { get; init; }
        public DateTimeOffset ExpiresOnUtc { get; init; }
        public DateTimeOffset CreatedOnUtc { get; init; } = DateTimeOffset.UtcNow;
        public TimeSpan Length => ExpiresOnUtc - CreatedOnUtc;
        public TimeSpan Remaining => ExpiresOnUtc - DateTimeOffset.UtcNow;

        [JsonIgnore]
        public bool IsPending => ExpiresOnUtc > DateTimeOffset.UtcNow;

        [JsonIgnore]
        public bool IsExpired => !IsPending;

        public bool Matches(string value)
        {
            return Filter is null || FileSystemName.MatchesSimpleExpression(Filter, value);
        }

        public static implicit operator bool(Data data) => data.IsPending;
    }
}