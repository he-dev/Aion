using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Util;
using Aion.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Core.Maintenance;

public class MaintenanceDirectory(ILogger<MaintenanceDirectory> logger, IOptions<MaintenanceTokenOptions> options)
{
    private SemaphoreSlim Gate { get; } = new(initialCount: 1, maxCount: 1);

    private DirectoryTree DirectoryTree { get; } = new(VariableTemplate.Render(options.Value.PendingPath, []));

    /// <summary>
    /// Sets or clears the pause expiry for a job.
    /// </summary>
    public async Task<MaintenanceToken> Create(string? filter, DateTimeOffset startsOnUtc, DateTimeOffset expiresOnUtc)
    {
        var token = new MaintenanceToken
        {
            Filter = filter ?? "*",
            StartsOnUtc = startsOnUtc,
            ExpiresOnUtc = expiresOnUtc,
        };

        if (token.IsExpired)
        {
            throw new ArgumentException("Maintenance token expiry must be in the future.", nameof(expiresOnUtc));
        }

        var startsOnStr = token.StartsOnUtc.ToString("yyyyMMdd_HHmm", System.Globalization.CultureInfo.InvariantCulture);
        var expiresOnStr = token.ExpiresOnUtc.ToString("yyyyMMdd_HHmm", System.Globalization.CultureInfo.InvariantCulture);
        var fileName = $"maintenance_between_{startsOnStr}_{expiresOnStr}.json";
        var filePath = Path.Join(DirectoryTree.Path, fileName);

        try
        {
            Directory.CreateDirectory(DirectoryTree.Path);

            // .. Atomic-write: first use the temp-file, then replace the original.
            // ?? Use a block-using so that the file-stream is released for the move. Otherwise, it remains locked and fails.
            await using (var fileStream = new FileStream(filePath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(fileStream, token, new JsonSerializerOptions { WriteIndented = true });
            }

            File.Move(filePath + ".tmp", filePath, overwrite: true);

            logger.LogDebug("Created maintenance token '{filter}' at '{filePath}'.", token.Filter, filePath);
            logger.LogInformation("Maintenance token '{filter}' starts at {startsOnUtc} and expires at {expiresOnUtc} minutes.", token.Filter, startsOnUtc, expiresOnUtc);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating maintenance token '{filter}' at '{filePath}'.", token.Filter, filePath);
            throw;
        }

        return token;
    }

    /// <summary>
    /// Gets the pause expiry timestamp for a specific job.
    /// </summary>
    public async Task<IImmutableList<MaintenanceToken>> Pending()
    {
        // !! Let only a single caller at a time search for tokens.
        await Gate.WaitAsync();

        var expiredPath =
            options.Value.ExpiredPath is not null
                ? VariableTemplate.Render(options.Value.ExpiredPath, [])
                : null;

        var pendingTokens = ImmutableList<MaintenanceToken>.Empty;

        try
        {
            // .. Check all tokens in that directory.
            foreach (var filePath in DirectoryTree.First().Files())
            {
                if (!File.Exists(filePath))
                {
                    // .. The file was deleted while we were iterating.
                    continue;
                }

                try
                {
                    if (await FromFile(filePath) is { } token)
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
                            // .. Collect pending tokens.
                            pendingTokens = pendingTokens.Add(token);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error reading maintenance token '{filePath}'.", filePath);
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
    private static async Task<MaintenanceToken?> FromFile(string filePath)
    {
        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<MaintenanceToken>(fileStream);
    }

}