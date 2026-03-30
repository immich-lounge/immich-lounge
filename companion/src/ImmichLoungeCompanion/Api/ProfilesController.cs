using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using ImmichLoungeCompanion.Services;
using ImmichLoungeCompanion.Storage;
using Microsoft.AspNetCore.Mvc;

namespace ImmichLoungeCompanion.Api;

[ApiController]
[Route("api/profiles")]
public class ProfilesController(
    IProfileRepository profiles,
    IPlaylistCache cache,
    IPlaylistBuilder playlistBuilder,
    IProfileValidator profileValidator,
    IProfileDocumentService profileDocuments) : ControllerBase
{
    private static readonly Regex ValidId = new(@"^[a-z0-9][a-z0-9\-]{1,62}[a-z0-9]$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var all = await profiles.GetAllAsync();
        return Ok(all.Select(p => new { p.Id, p.Name, p.Description }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Profile profile)
    {
        profileDocuments.Normalize(profile);

        if (string.IsNullOrEmpty(profile.Id) || !ValidId.IsMatch(profile.Id))
        {
            return BadRequest(new { error = "Invalid profile id. Use lowercase alphanumeric and hyphens, 3–64 chars." });
        }

        if (await profiles.ExistsAsync(profile.Id))
        {
            return Conflict(new { error = "Profile id already exists." });
        }

        var validationError = profileValidator.Validate(profile);
        if (validationError != null)
        {
            return BadRequest(new { error = validationError });
        }

        await profiles.SaveAsync(profile);
        return Created($"/api/profiles/{profile.Id}", await profileDocuments.BuildResponseAsync(profile));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var profile = await profiles.GetAsync(id);
        if (profile == null)
        {
            return NotFound(new { error = "Profile not found." });
        }

        return Ok(await profileDocuments.BuildResponseAsync(profile));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] JsonElement body)
    {
        var existing = await profiles.GetAsync(id);
        if (existing == null)
        {
            return NotFound(new { error = "Profile not found." });
        }

        if (!IsCompleteProfilePayload(body))
        {
            return BadRequest(new { error = "Partial profile updates are not supported. Send the full profile document." });
        }

        var json = body.GetRawText();
        var updated = JsonSerializer.Deserialize<Profile>(json, JsonOptions)!;
        updated.Id = id; // URL id is authoritative
        profileDocuments.Normalize(updated);

        var validationError = profileValidator.Validate(updated);
        if (validationError != null)
        {
            return BadRequest(new { error = validationError });
        }

        await profiles.SaveAsync(updated);
        cache.Invalidate(id);
        return Ok(await profileDocuments.BuildResponseAsync(updated));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await profiles.DeleteAsync(id);
        if (!deleted)
        {
            return NotFound(new { error = "Profile not found." });
        }

        cache.Invalidate(id);
        return NoContent();
    }

    [HttpPost("preview-count")]
    public async Task<IActionResult> PreviewCount([FromBody] Profile profile)
    {
        profileDocuments.Normalize(profile);

        var validationError = profileValidator.Validate(profile);
        if (validationError != null)
        {
            return BadRequest(new { error = validationError });
        }

        var entries = await playlistBuilder.BuildAsync(profile);
        return Ok(new { matchedAssets = entries.Count });
    }

    private static bool IsCompleteProfilePayload(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return HasProperties(body, "name", "contentSources", "mediaTypes", "slideshow", "display", "imageQuality") &&
               HasObjectProperties(body, "mediaTypes", "photos", "videos", "videoAudio", "livePhotos") &&
               HasObjectProperties(body, "slideshow", "intervalSeconds", "shuffle", "transitionEffect", "photoMotion", "refreshIntervalMinutes", "preventScreensaver") &&
               HasObjectProperties(body, "display", "formatSource", "overlayStyle", "backgroundEffect", "overlayFields", "overlayBehavior", "overlayFadeSeconds", "clockAlwaysVisible", "clockFormat", "weatherUnit", "showTimer", "showDate", "dateFormat", "locale");
    }

    private static bool HasProperties(JsonElement body, params string[] names) =>
        names.All(name => body.TryGetProperty(name, out _));

    private static bool HasObjectProperties(JsonElement body, string propertyName, params string[] nestedNames)
    {
        if (!body.TryGetProperty(propertyName, out var nested) || nested.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return nestedNames.All(name => nested.TryGetProperty(name, out _));
    }
}
