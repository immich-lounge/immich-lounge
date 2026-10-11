using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using Microsoft.Extensions.Logging;

namespace ImmichLoungeCompanion.Immich;

public class ImmichClient(IHttpClientFactory httpClientFactory, ILogger<ImmichClient> logger, TimeProvider? timeProvider = null) : IImmichClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim _capabilityLock = new(1, 1);
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private string? _capabilityServer;
    private SearchApiCapabilities? _capabilities;
    private DateTimeOffset _capabilitiesExpire;

    public async Task<SearchApiCapabilities> GetSearchApiCapabilitiesAsync(ImmichSettings settings, bool refresh = false)
    {
        var (client, baseUrl) = BuildClient(settings);
        await _capabilityLock.WaitAsync();
        try
        {
            if (!refresh && _capabilityServer == baseUrl && _capabilities != null && _time.GetUtcNow() < _capabilitiesExpire)
            {
                return _capabilities;
            }

            SearchApiCapabilities detected;
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/server/version");
                using var response = await client.SendAsync(req, timeout.Token);
                response.EnsureSuccessStatusCode();
                var version = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, timeout.Token);
                var major = version.GetProperty("major").GetInt32();
                var minor = version.GetProperty("minor").GetInt32();
                var patch = version.GetProperty("patch").GetInt32();
                if (major < 0 || minor < 0 || patch < 0)
                {
                    throw new JsonException("Invalid server version.");
                }

                var prerelease = version.TryGetProperty("prerelease", out var pre) && pre.ValueKind != JsonValueKind.Null;
                var structured = major > 3 || (major == 3 && (minor > 2 || (minor == 2 && !prerelease)));
                detected = new($"{major}.{minor}.{patch}" + (prerelease ? "-prerelease" : ""), structured);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException
                or InvalidOperationException or FormatException or KeyNotFoundException)
            {
                logger.LogWarning(ex, "Unable to detect Immich search API; using legacy search.");
                detected = new(null, false, "Server version could not be detected. Using legacy search.");
            }

            // Only one configured server is retained; no unbounded cache or persisted version.
            _capabilityServer = baseUrl;
            _capabilities = detected;
            _capabilitiesExpire = _time.GetUtcNow().Add(detected.ServerVersion == null
                ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(5));
            return detected;
        }
        finally { _capabilityLock.Release(); }
    }

    // NOTE: Do NOT set BaseAddress or DefaultRequestHeaders on the shared named client —
    // the factory may return the same instance across calls and mutations are not thread-safe.
    // Instead, build per-request URIs and set x-api-key per request using HttpRequestMessage.
    private (HttpClient Client, string BaseUrl) BuildClient(ImmichSettings settings)
    {
        var client = httpClientFactory.CreateClient("immich");
        return (client, settings.ServerUrl.TrimEnd('/'));
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, string context)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        logger.LogError("Immich API error [{Context}] HTTP {Status}: {Body}",
            context, (int)response.StatusCode, body);
        response.EnsureSuccessStatusCode(); // throws with status code
    }

    private static HttpRequestMessage ApiRequest(HttpMethod method, string baseUrl, string path, string apiKey)
    {
        var req = new HttpRequestMessage(method, $"{baseUrl}/{path}");
        req.Headers.Add("x-api-key", apiKey);
        return req;
    }

    public async Task<(bool Ok, int ImageCount, int VideoCount, string? Error)> TestConnectionAsync(ImmichSettings settings)
    {
        try
        {
            var (client, baseUrl) = BuildClient(settings);

            // Step 1: ping (no auth) — validates the server URL
            var pingReq = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/server/ping");
            var pingResp = await client.SendAsync(pingReq);
            if (!pingResp.IsSuccessStatusCode)
            {
                return (false, 0, 0, $"Server unreachable (HTTP {(int)pingResp.StatusCode})");
            }

            // Step 2: asset statistics (requires auth) — validates API key, returns user's own asset counts
            var statsReq = ApiRequest(HttpMethod.Get, baseUrl, "api/assets/statistics", settings.ApiKey);
            var statsResp = await client.SendAsync(statsReq);
            if (!statsResp.IsSuccessStatusCode)
            {
                return (false, 0, 0, $"HTTP {(int)statsResp.StatusCode}");
            }

            var stats = await statsResp.Content.ReadFromJsonAsync<AssetStatisticsResponse>(JsonOptions);
            return (true, stats?.Images ?? 0, stats?.Videos ?? 0, null);
        }
        catch (Exception ex)
        {
            return (false, 0, 0, ex.Message);
        }
    }

    public async Task<List<ImmichAlbum>> GetAlbumsAsync(ImmichSettings settings)
    {
        var (client, baseUrl) = BuildClient(settings);
        // Immich returns albums owned by or shared with the configured user.
        return await GetAlbumsPageAsync(client, baseUrl, settings.ApiKey, "api/albums", "GET /api/albums");
    }

    public async Task<List<ImmichPerson>> GetPeopleAsync(ImmichSettings settings)
    {
        var (client, baseUrl) = BuildClient(settings);
        var req = ApiRequest(HttpMethod.Get, baseUrl, "api/people", settings.ApiKey);
        var response = await client.SendAsync(req);
        await EnsureSuccessAsync(response, "GET /api/people");
        // Immich returns { people: [...], total, hasNextPage }
        var wrapper = await response.Content.ReadFromJsonAsync<PeopleResponse>(JsonOptions);
        return wrapper?.People ?? [];
    }

    public async Task<List<string>> GetLocationSuggestionsAsync(ImmichSettings settings, string type,
        string? country = null, string? state = null)
    {
        if (type is not ("country" or "state" or "city"))
        {
            throw new ArgumentException("Invalid location type.", nameof(type));
        }

        var (client, baseUrl) = BuildClient(settings);
        var path = $"api/search/suggestions?type={type}";
        if (!string.IsNullOrWhiteSpace(country))
        {
            path += $"&country={Uri.EscapeDataString(country)}";
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            path += $"&state={Uri.EscapeDataString(state)}";
        }

        using var req = ApiRequest(HttpMethod.Get, baseUrl, path, settings.ApiKey);
        using var response = await client.SendAsync(req);
        await EnsureSuccessAsync(response, "GET /api/search/suggestions");
        return await response.Content.ReadFromJsonAsync<List<string>>(JsonOptions) ?? [];
    }

    public async Task<List<ImmichTag>> GetTagsAsync(ImmichSettings settings)
    {
        var (client, baseUrl) = BuildClient(settings);
        var req = ApiRequest(HttpMethod.Get, baseUrl, "api/tags", settings.ApiKey);
        var response = await client.SendAsync(req);
        await EnsureSuccessAsync(response, "GET /api/tags");
        return await response.Content.ReadFromJsonAsync<List<ImmichTag>>(JsonOptions) ?? [];
    }

    public async Task<List<ImmichAsset>> SearchAssetsAllPagesAsync(
        ImmichSettings settings, SearchAssetsRequest request)
    {
        var (client, baseUrl) = BuildClient(settings);
        var capabilities = await GetSearchApiCapabilitiesAsync(settings);
        var allAssets = new List<ImmichAsset>();
        var page = 1;
        string? cursor = null;
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            var body = SearchRequestSerializer.Serialize(request, capabilities.StructuredSearch, page, cursor, JsonOptions);
            using var req = ApiRequest(HttpMethod.Post, baseUrl, "api/search/metadata", settings.ApiKey);
            req.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(req);
            // Search errors must remain errors. Retrying with fewer filters could broaden a playlist.
            await EnsureSuccessAsync(response, $"POST /api/search/metadata page={page}");
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Object ||
                !assets.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array ||
                (capabilities.StructuredSearch && !assets.TryGetProperty("nextCursor", out _)))
            {
                // Older servers may ignore unknown structured fields. Reject that response rather
                // than display a broadened playlist after a downgrade or a stale version report.
                await GetSearchApiCapabilitiesAsync(settings, refresh: true);
                throw new JsonException("Immich returned an incompatible search response. Server version detection has been refreshed.");
            }
            var result = root.Deserialize<SearchAssetsResponse>(JsonOptions)
                ?? throw new JsonException("Immich returned an empty search response.");
            if (capabilities.StructuredSearch && result.Assets.NextPage != null)
            {
                await GetSearchApiCapabilitiesAsync(settings, refresh: true);
                throw new JsonException("Immich returned numbered pages for a structured search.");
            }
            allAssets.AddRange(result.Assets.Items);

            if (capabilities.StructuredSearch)
            {
                if (result.Assets.NextCursor == null)
                {
                    break;
                }

                cursor = result.Assets.NextCursor;
                if (string.IsNullOrEmpty(cursor) || !seenCursors.Add(cursor))
                {
                    throw new JsonException("Immich returned an invalid or repeated search cursor.");
                }
            }
            else
            {
                if (result.Assets.NextPage == null)
                {
                    break;
                }

                if (!int.TryParse(result.Assets.NextPage, out var nextPage) || nextPage <= page)
                {
                    throw new JsonException("Immich returned an invalid or repeated search page.");
                }

                page = nextPage;
            }
        }
        return allAssets;
    }

    public async Task<ImmichAsset?> GetAssetAsync(ImmichSettings settings, string assetId, CancellationToken ct = default)
    {
        var (client, baseUrl) = BuildClient(settings);
        using var req = ApiRequest(HttpMethod.Get, baseUrl, $"api/assets/{Uri.EscapeDataString(assetId)}", settings.ApiKey);
        using var response = await client.SendAsync(req, ct);
        // A photo deleted between memory selection and hydration should be skipped.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        await EnsureSuccessAsync(response, "GET /api/assets/{id}");
        return await response.Content.ReadFromJsonAsync<ImmichAsset>(JsonOptions, ct);
    }

    public async Task<List<ImmichMemory>> GetMemoriesAsync(ImmichSettings settings, DateOnly date)
    {
        var (client, baseUrl) = BuildClient(settings);
        var req = ApiRequest(HttpMethod.Get, baseUrl, $"api/memories?for={date:yyyy-MM-dd}", settings.ApiKey);
        var response = await client.SendAsync(req);
        await EnsureSuccessAsync(response, $"GET /api/memories?for={date:yyyy-MM-dd}");
        return await response.Content.ReadFromJsonAsync<List<ImmichMemory>>(JsonOptions) ?? [];
    }

    private class PeopleResponse
    {
        public List<ImmichPerson> People { get; set; } = [];
        public int Total { get; set; }
    }

    private async Task<List<ImmichAlbum>> GetAlbumsPageAsync(
        HttpClient client,
        string baseUrl,
        string apiKey,
        string path,
        string context)
    {
        var req = ApiRequest(HttpMethod.Get, baseUrl, path, apiKey);
        var response = await client.SendAsync(req);
        await EnsureSuccessAsync(response, context);
        return await response.Content.ReadFromJsonAsync<List<ImmichAlbum>>(JsonOptions) ?? [];
    }

    private async Task<List<ImmichAlbum>> GetSharedAlbumsAsync(HttpClient client, string baseUrl, string apiKey)
    {
        const string path = "api/albums?shared=true";
        const string context = "GET /api/albums?shared=true";

        var req = ApiRequest(HttpMethod.Get, baseUrl, path, apiKey);
        var response = await client.SendAsync(req);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<ImmichAlbum>>(JsonOptions) ?? [];
        }

        var body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
        {
            logger.LogWarning(
                "Immich shared albums query rejected [{Context}] HTTP {Status}: {Body}",
                context, (int)response.StatusCode, body);
            logger.LogWarning("Immich shared albums query is unavailable; continuing with own albums only.");
            return [];
        }

        logger.LogError("Immich API error [{Context}] HTTP {Status}: {Body}",
            context, (int)response.StatusCode, body);
        response.EnsureSuccessStatusCode();
        return [];
    }

    private async Task NormalizeAlbumCountsAsync(HttpClient client, string baseUrl, string apiKey, List<ImmichAlbum> albums)
    {
        foreach (var album in albums)
        {
            if (album.AssetCount > 0)
            {
                continue;
            }

            if (album.Assets.Count > 0)
            {
                album.AssetCount = album.Assets.Count;
                continue;
            }

            var path = $"api/albums/{Uri.EscapeDataString(album.Id)}?withoutAssets=true";
            var req = ApiRequest(HttpMethod.Get, baseUrl, path, apiKey);
            var response = await client.SendAsync(req);
            if (response.IsSuccessStatusCode)
            {
                var detailedAlbum = await response.Content.ReadFromJsonAsync<ImmichAlbum>(JsonOptions);
                if (detailedAlbum is not null)
                {
                    album.AssetCount = detailedAlbum.AssetCount;
                }
                continue;
            }

            var body = await response.Content.ReadAsStringAsync();
            logger.LogWarning(
                "Immich album count query rejected [GET /api/albums/{AlbumId}?withoutAssets=true] HTTP {Status}: {Body}",
                album.Id, (int)response.StatusCode, body);
        }
    }

    private class AssetStatisticsResponse
    {
        public int Images { get; set; }
        public int Videos { get; set; }
    }
}
