using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using ImmichLoungeCompanion.Storage;
using Microsoft.AspNetCore.Mvc;

namespace ImmichLoungeCompanion.Api;

[ApiController]
[Route("api/settings")]
public class SettingsController(ISettingsRepository settings, IPlaylistCache cache) : ControllerBase
{
    [HttpGet]
    public async Task<GlobalSettings> Get() => await settings.LoadAsync();

    [HttpPut]
    public async Task<GlobalSettings> Put([FromBody] GlobalSettings updated)
    {
        // Preserve CompanionUuid — never overwritten from request body
        var existing = await settings.LoadAsync();
        updated.CompanionUuid = existing.CompanionUuid;
        await settings.SaveAsync(updated);
        // Immich URL or API key may have changed — all cached playlists are stale
        cache.InvalidateAll();
        return updated;
    }
}
