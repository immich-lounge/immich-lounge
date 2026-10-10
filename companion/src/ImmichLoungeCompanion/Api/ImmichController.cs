using System.Linq;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Storage;
using Microsoft.AspNetCore.Mvc;

namespace ImmichLoungeCompanion.Api;

[ApiController]
[Route("api/immich")]
public class ImmichController(IImmichClient immich, ISettingsRepository settings) : ControllerBase
{
    [HttpGet("capabilities")]
    public async Task<IActionResult> Capabilities()
    {
        var s = await settings.LoadAsync();
        return Ok(await immich.GetSearchApiCapabilitiesAsync(s.Immich));
    }

    [HttpGet("test")]
    public async Task<IActionResult> Test()
    {
        var s = await settings.LoadAsync();
        var (ok, imageCount, videoCount, error) = await immich.TestConnectionAsync(s.Immich);
        var searchApi = ok ? await immich.GetSearchApiCapabilitiesAsync(s.Immich, refresh: true) : null;
        return Ok(new { ok, imageCount, videoCount, error, searchApi });
    }

    [HttpGet("albums")]
    public async Task<IActionResult> Albums()
    {
        var s = await settings.LoadAsync();
        var albums = await immich.GetAlbumsAsync(s.Immich);
        return Ok(albums.Select(a => new { id = a.Id, name = a.AlbumName, assetCount = a.AssetCount }));
    }

    [HttpGet("people")]
    public async Task<IActionResult> People()
    {
        var s = await settings.LoadAsync();
        var people = await immich.GetPeopleAsync(s.Immich);
        return Ok(people.Select(p => new
        {
            id = p.Id,
            name = p.Name,
            thumbnailUrl = p.ThumbnailPath != null
                ? $"{s.Immich.ServerUrl.TrimEnd('/')}/{p.ThumbnailPath.TrimStart('/')}"
                : null
        }));
    }

    [HttpGet("locations")]
    public async Task<IActionResult> Locations(string type, string? country = null, string? state = null)
    {
        if (type is not ("country" or "state" or "city"))
        {
            return BadRequest(new { error = "Location type must be country, state, or city." });
        }

        var s = await settings.LoadAsync();
        return Ok(await immich.GetLocationSuggestionsAsync(s.Immich, type, country, state));
    }

    [HttpGet("tags")]
    public async Task<IActionResult> Tags()
    {
        var s = await settings.LoadAsync();
        var tags = await immich.GetTagsAsync(s.Immich);
        return Ok(tags.Select(t => new { id = t.Id, name = t.Value ?? t.Name }));
    }
}
