using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Storage;
using ImmichLoungeCompanion.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ImmichLoungeCompanion.Playlist;

public class PlaylistCacheWorker(
    IPlaylistBuilder builder,
    IPlaylistCache cache,
    IProfileRepository profiles,
    ILogger<PlaylistCacheWorker> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
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
            // Capture before reading either the profile or the builder's connection settings.
            var generation = cache.GetGeneration(profileId);
            var profile = await profiles.GetAsync(profileId);
            if (profile == null)
            {
                return;
            }

            var existing = cache.Get(profileId);
            // Stamp the build's day before fetching, so a build crossing midnight is refreshed.
            var startedAt = _time.GetUtcNow();
            var assets = await builder.BuildAsync(profile, ct);
            var memoryDayChanged = profile.PhotoSource != null && profile.HasMemoriesSource() && existing != null &&
                MemoryDate.For(profile, existing.GeneratedAt) != MemoryDate.For(profile, startedAt);
            if (profile.Slideshow.Shuffle && existing?.Assets.Count > 0 && !memoryDayChanged)
            {
                assets = PlaylistShuffleOrder.PreserveExistingOrder(existing.Assets, assets);
            }
            assets = PlaylistEntryProjector.Prioritize(assets, profile);

            var playlistVersion = existing?.PlaylistVersion;
            if (string.IsNullOrWhiteSpace(playlistVersion) ||
                existing == null || memoryDayChanged ||
                !existing.Assets.SequenceEqual(assets))
            {
                playlistVersion = Guid.NewGuid().ToString("N");
            }

            var generatedAt = profile.HasMemoriesSource() ? startedAt : _time.GetUtcNow();
            if (!cache.TrySet(profileId, new(assets, generatedAt, playlistVersion), generation))
            {
                logger.LogInformation("Discarded invalidated playlist build for profile {ProfileId}", profileId);
                return;
            }
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
        List<Profile> allProfiles;
        try
        {
            allProfiles = await profiles.GetAllAsync();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogError(ex, "Could not read profiles for playlist refresh; retrying on the next refresh cycle");
            return;
        }
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
            var now = _time.GetUtcNow();
            var age = now - entry.GeneratedAt;
            var memoryDayChanged = profile.HasMemoriesSource() &&
                MemoryDate.For(profile, entry.GeneratedAt) != MemoryDate.For(profile, now);

            if (memoryDayChanged || age >= interval - rebuildWindow)
            {
                _ = Task.Run(() => RebuildAsync(profile.Id, false, ct), ct);
            }
        }
    }
}
