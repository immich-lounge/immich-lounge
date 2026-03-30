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
    [HttpGet("test")]
    public async Task<IActionResult> Test()
    {
        var s = await settings.LoadAsync();
        var (ok, imageCount, videoCount, error) = await immich.TestConnectionAsync(s.Immich);
        return Ok(new { ok, imageCount, videoCount, error });
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

    [HttpGet("tags")]
    public async Task<IActionResult> Tags()
    {
        var s = await settings.LoadAsync();
        var tags = await immich.GetTagsAsync(s.Immich);
        return Ok(tags.Select(t => new { id = t.Id, name = t.Value ?? t.Name }));
    }
}
