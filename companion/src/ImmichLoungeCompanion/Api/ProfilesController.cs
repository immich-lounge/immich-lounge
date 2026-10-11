using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
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
    IProfileDocumentService profileDocuments,
    ISettingsRepository settings,
    IHttpClientFactory httpFactory) : ControllerBase
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
    public async Task<IActionResult> PreviewCount([FromBody] Profile profile, CancellationToken cancellationToken)
    {
        profileDocuments.Normalize(profile);

        var validationError = profileValidator.Validate(profile);
        if (validationError != null)
        {
            return BadRequest(new { error = validationError });
        }

        var entries = await playlistBuilder.BuildAsync(profile);
        var connection = (await settings.LoadAsync()).Immich;
        var samples = await Task.WhenAll(entries.Where(entry => entry.Type is "photo" or "livePhoto")
            .Where(entry => Guid.TryParse(entry.Id, out _))
            .Take(6)
            .Select(async entry => new
            {
                entry.Id,
                entry.SourceLabel,
                thumbnailUrl = await ReadThumbnailAsync(connection, Guid.Parse(entry.Id), cancellationToken)
            }));
        return Ok(new
        {
            matchedAssets = entries.Count,
            previewAssets = samples
        });
    }

    // Small, bounded samples are returned only by an explicit preview request.
    // No asset-serving endpoint or Immich credential is exposed to the browser.
    private async Task<string?> ReadThumbnailAsync(ImmichSettings connection, Guid assetId, CancellationToken cancellationToken)
    {
        const long maxBytes = 512 * 1024;
        if (string.IsNullOrWhiteSpace(connection.ApiKey) ||
            !Uri.TryCreate(connection.ServerUrl, UriKind.Absolute, out var server) ||
            server.Scheme is not ("http" or "https")) { return null; }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{connection.ServerUrl.TrimEnd('/')}/api/assets/{assetId:D}/thumbnail?size=thumbnail");
        request.Headers.Add("x-api-key", connection.ApiKey);
        try
        {
            using var response = await httpFactory.CreateClient("immich").SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) { return null; }
            var mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
            if (mediaType is not ("image/jpeg" or "image/png" or "image/webp" or "image/avif") ||
                response.Content.Headers.ContentLength > maxBytes) { return null; }
            await response.Content.LoadIntoBufferAsync(maxBytes, timeout.Token);
            var bytes = await response.Content.ReadAsByteArrayAsync(timeout.Token);
            return $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}";
        }
        catch (HttpRequestException) { return null; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
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
