using System;
using System.Threading;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ImmichLoungeCompanion.Playlist;

public class PlaylistCacheWorker(
    IPlaylistBuilder builder,
    IPlaylistCache cache,
    IProfileRepository profiles,
    ILogger<PlaylistCacheWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            await RebuildExpiredCachesAsync(stoppingToken);
        }
    }

    public async Task RebuildAsync(string profileId, bool buildingAlreadyMarked = false, CancellationToken ct = default)
    {
        if (!buildingAlreadyMarked && !cache.TryStartBuilding(profileId))
        {
            return;
        }

        try
        {
            var profile = await profiles.GetAsync(profileId);
            if (profile == null)
            {
                return;
            }

            var assets = await builder.BuildAsync(profile, ct);
            cache.Set(profileId, new(assets, DateTimeOffset.UtcNow));
            logger.LogInformation("Playlist cache rebuilt for profile {ProfileId}: {Count} entries", profileId, assets.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build playlist for profile {ProfileId}", profileId);
        }
        finally
        {
            cache.ClearBuilding(profileId);
        }
    }

    private async Task RebuildExpiredCachesAsync(CancellationToken ct)
    {
        var allProfiles = await profiles.GetAllAsync();
        foreach (var profile in allProfiles)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            var entry = cache.Get(profile.Id);
            if (entry == null)
            {
                continue; // cold — will be built on first request
            }

            var interval = TimeSpan.FromMinutes(profile.Slideshow.RefreshIntervalMinutes);
            var rebuildWindow = TimeSpan.FromMinutes(
                Math.Clamp(profile.Slideshow.RefreshIntervalMinutes * 0.2, 1, 10));
            var age = DateTimeOffset.UtcNow - entry.GeneratedAt;

            if (age >= interval - rebuildWindow)
            {
                _ = Task.Run(() => RebuildAsync(profile.Id, false, ct), ct);
            }
        }
    }
}
