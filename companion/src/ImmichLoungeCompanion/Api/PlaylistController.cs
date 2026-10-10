using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using ImmichLoungeCompanion.Storage;
using Microsoft.AspNetCore.Mvc;

namespace ImmichLoungeCompanion.Api;

[ApiController]
[Route("api/playlists")]
public class PlaylistsController(IPlaylistCache cache) : ControllerBase
{
    [HttpDelete]
    public IActionResult InvalidateAll()
    {
        cache.InvalidateAll();
        return NoContent();
    }
}

[ApiController]
[Route("api/profiles/{id}/playlist")]
public class PlaylistController(
    IPlaylistCache cache,
    PlaylistCacheWorker worker,
    IProfileRepository profiles) : ControllerBase
{
    private static string EffectivePlaylistVersion(PlaylistCacheEntry entry)
        => string.IsNullOrWhiteSpace(entry.PlaylistVersion)
            ? $"legacy-{entry.GeneratedAt.ToUnixTimeMilliseconds()}"
            : entry.PlaylistVersion;

    private static (string? Version, int Seed) ParseCycleVersion(string? version)
    {
        var separator = version?.LastIndexOf('~') ?? -1;
        if (separator > 0 && int.TryParse(version![(separator + 1)..],
                NumberStyles.None, CultureInfo.InvariantCulture, out var seed) && seed > 0)
        {
            return (version![..separator], seed);
        }
        return (version, 0);
    }

    [HttpDelete]
    public IActionResult Invalidate(string id)
    {
        cache.Invalidate(id);
        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> Get(string id, [FromQuery] int count = 500, [FromQuery] int offset = 0, [FromQuery] string? playlistVersion = null)
    {
        count = Math.Clamp(count, 1, 1000);
        var profile = await profiles.GetAsync(id);
        if (profile == null)
        {
            return NotFound(new { error = "Profile not found." });
        }

        var cached = cache.Get(id);
        if (cached != null)
        {
            var effectivePlaylistVersion = EffectivePlaylistVersion(cached);
            var (requestedVersion, cycleSeed) = ParseCycleVersion(playlistVersion);
            if (!profile.Slideshow.Shuffle)
            {
                cycleSeed = 0;
            }
            var totalCount = cached.Assets.Count;
            if (totalCount == 0)
            {
                return Ok(new PlaylistResponse
                {
                    Assets = [],
                    GeneratedAt = cached.GeneratedAt,
                    PlaylistVersion = effectivePlaylistVersion,
                    Cached = true,
                    Building = false,
                    Offset = 0,
                    NextOffset = 0,
                    TotalCount = 0
                });
            }

            if (!string.IsNullOrWhiteSpace(requestedVersion) &&
                !string.Equals(requestedVersion, effectivePlaylistVersion, StringComparison.Ordinal))
            {
                offset = 0;
            }

            offset = ((offset % totalCount) + totalCount) % totalCount;
            var orderedAssets = cycleSeed > 0
                ? PlaylistShuffleOrder.ForCycle(cached.Assets, cycleSeed)
                : cached.Assets;
            if (cycleSeed > 0)
            {
                effectivePlaylistVersion += "~" + cycleSeed.ToString(CultureInfo.InvariantCulture);
            }
            var assets = orderedAssets
                .Skip(offset)
                .Take(count)
                .ToList();
            var nextOffset = (offset + assets.Count) % totalCount;

            return Ok(new PlaylistResponse
            {
                Assets = assets,
                GeneratedAt = cached.GeneratedAt,
                PlaylistVersion = effectivePlaylistVersion,
                Cached = true,
                Building = false,
                Offset = offset,
                NextOffset = nextOffset,
                TotalCount = totalCount
            });
        }

        // Cold cache: start background build, return building response
        if (cache.TryStartBuilding(id))
        {
            _ = Task.Run(() => worker.RebuildAsync(id, true));
        }

        return Ok(new PlaylistResponse
        {
            Assets = [],
            GeneratedAt = null,
            PlaylistVersion = null,
            Cached = false,
            Building = true,
            Offset = 0,
            NextOffset = 0,
            TotalCount = 0
        });
    }
}
