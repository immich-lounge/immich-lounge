using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.Api;

[TestClass]
public class ProfilesApiTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public ProfilesApiTests() => _client = _factory.CreateClient();
    public void Dispose() => _factory.Dispose();

    private static object NewProfileBody(string id, object[]? contentSources = null) => new
    {
        id,
        name = "Test " + id,
        description = "A test profile",
        contentSources = contentSources ?? Array.Empty<object>(),
        mediaTypes = new { photos = true, videos = false, videoAudio = false, livePhotos = false },
        slideshow = new { intervalSeconds = 10, shuffle = true, transitionEffect = "fade", refreshIntervalMinutes = 60 },
        display = new { formatSource = "roku", overlayStyle = "bottom", backgroundEffect = "blur", overlayFields = new[] { "date" }, overlayBehavior = "fade", overlayFadeSeconds = 5, clockAlwaysVisible = true, clockFormat = "HH:mm", weatherUnit = "celsius", showTimer = true, showDate = true, dateFormat = "d MMMM yyyy", locale = "en-US" },
        imageQuality = "preview"
    };

    [TestMethod]
    public async Task GetProfiles_WhenNone_ReturnsEmpty()
    {
        var response = await _client.GetAsync("/api/profiles");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<object>>();
        Assert.AreEqual(0, list!.Count);
    }

    [TestMethod]
    public async Task PostProfile_Creates_Returns201WithEnrichedProfile()
    {
        var response = await _client.PostAsJsonAsync("/api/profiles", NewProfileBody("living-room",
        [
            new { type = "album", id = "a1", label = "Family" }
        ]));
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Profile>();
        Assert.AreEqual("living-room", body!.Id);
        Assert.IsNotNull(body);
        Assert.IsNotNull(body.AssetFilter);
        Assert.AreEqual(AssetFilterRuleKind.Group, body.AssetFilter.Kind);
        Assert.AreEqual(AssetFilterGroupOperator.Or, body.AssetFilter.Operator);
    }

    [TestMethod]
    public async Task PostProfile_DuplicateId_Returns409()
    {
        await _client.PostAsJsonAsync("/api/profiles", NewProfileBody("dup"));
        var response = await _client.PostAsJsonAsync("/api/profiles", NewProfileBody("dup"));
        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
    }

    [TestMethod]
    public async Task PostProfile_InvalidId_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/profiles", NewProfileBody("INVALID ID!"));
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetProfile_NotFound_Returns404()
    {
        var response = await _client.GetAsync("/api/profiles/nope");
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task PutProfile_WithPartialBody_Returns400()
    {
        await _client.PostAsJsonAsync("/api/profiles", NewProfileBody("edit-me"));
        var updated = new { name = "Updated", imageQuality = "original" };
        var response = await _client.PutAsJsonAsync("/api/profiles/edit-me", updated);
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task PutProfile_WithFullBody_UpdatesProfile()
    {
        await _client.PostAsJsonAsync("/api/profiles", NewProfileBody("edit-me"));

        var response = await _client.PutAsJsonAsync("/api/profiles/edit-me", new
        {
            id = "ignored-by-route",
            schemaVersion = 1,
            name = "Updated",
            description = "Updated description",
            contentSources = Array.Empty<object>(),
            mediaTypes = new { photos = true, videos = false, videoAudio = false, livePhotos = false },
            slideshow = new
            {
                intervalSeconds = 12,
                shuffle = false,
                transitionEffect = "fade",
                photoMotion = "none",
                refreshIntervalMinutes = 60,
                preventScreensaver = false
            },
            display = new
            {
                formatSource = "profile",
                overlayStyle = "bottom",
                backgroundEffect = "blur",
                overlayFields = new[] { "date" },
                overlayBehavior = "fade",
                overlayFadeSeconds = 5,
                clockAlwaysVisible = true,
                clockFormat = "HH:mm",
                weatherUnit = "celsius",
                showTimer = true,
                showDate = true,
                dateFormat = "d MMMM yyyy",
                locale = "en-US"
            },
            imageQuality = "original",
            dateFilter = (object?)null,
            weather = (object?)null
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Profile>();
        Assert.IsNotNull(body);
        Assert.AreEqual("edit-me", body.Id);
        Assert.AreEqual("Updated", body.Name);
        Assert.AreEqual("original", body.ImageQuality);
        Assert.IsFalse(body.Slideshow.Shuffle);
    }

    [TestMethod]
    public async Task DeleteProfile_RemovesProfile()
    {
        await _client.PostAsJsonAsync("/api/profiles", NewProfileBody("del-me"));
        var response = await _client.DeleteAsync("/api/profiles/del-me");
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        var get = await _client.GetAsync("/api/profiles/del-me");
        Assert.AreEqual(HttpStatusCode.NotFound, get.StatusCode);
    }

    [TestMethod]
    public async Task PreviewCount_ReturnsMatchedAssetCount()
    {
        _factory.ImmichClient.SearchAssetsAllPagesAsync(Arg.Any<ImmichSettings>(), Arg.Any<SearchAssetsRequest>())
            .Returns(new List<ImmichAsset>
            {
                new() { Id = "p1", Type = "IMAGE" },
                new() { Id = "p2", Type = "IMAGE" }
            });

        var response = await _client.PostAsJsonAsync("/api/profiles/preview-count", NewProfileBody("preview"));
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(2, body.GetProperty("matchedAssets").GetInt32());
    }

    [TestMethod]
    public async Task PutProfile_WithAssetFilter_PersistsStringEnums()
    {
        await _client.PostAsJsonAsync("/api/profiles", NewProfileBody("rules"));

        var response = await _client.PutAsJsonAsync("/api/profiles/rules", new
        {
            id = "ignored-by-route",
            schemaVersion = 1,
            name = "Rules",
            description = "Updated description",
            contentSources = Array.Empty<object>(),
            assetFilter = new
            {
                kind = "group",
                @operator = "and",
                children = new object[]
                {
                    new
                    {
                        kind = "condition",
                        type = "person",
                        id = "p1",
                        label = "Alice"
                    },
                    new
                    {
                        kind = "condition",
                        type = "album",
                        id = "a1",
                        label = "Family"
                    }
                }
            },
            mediaTypes = new { photos = true, videos = false, videoAudio = false, livePhotos = false },
            slideshow = new
            {
                intervalSeconds = 12,
                shuffle = false,
                transitionEffect = "fade",
                photoMotion = "none",
                refreshIntervalMinutes = 60,
                preventScreensaver = false
            },
            display = new
            {
                formatSource = "profile",
                overlayStyle = "bottom",
                backgroundEffect = "blur",
                overlayFields = new[] { "date" },
                overlayBehavior = "fade",
                overlayFadeSeconds = 5,
                clockAlwaysVisible = true,
                clockFormat = "HH:mm",
                weatherUnit = "celsius",
                showTimer = true,
                showDate = true,
                dateFormat = "d MMMM yyyy",
                locale = "en-US"
            },
            imageQuality = "original",
            dateFilter = (object?)null,
            weather = (object?)null
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Profile>();
        Assert.IsNotNull(body?.AssetFilter);
        Assert.AreEqual(AssetFilterRuleKind.Group, body.AssetFilter.Kind);
        Assert.AreEqual(AssetFilterGroupOperator.And, body.AssetFilter.Operator);
        Assert.AreEqual(2, body.AssetFilter.Children.Count);
        Assert.AreEqual(AssetFilterConditionType.Person, body.AssetFilter.Children[0].Type);
    }

    [TestMethod]
    public async Task PreviewCount_WithInvalidAssetFilter_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/profiles/preview-count", new
        {
            id = "preview",
            name = "Preview",
            description = "A test profile",
            contentSources = Array.Empty<object>(),
            assetFilter = new
            {
                kind = "group",
                children = Array.Empty<object>()
            },
            mediaTypes = new { photos = true, videos = false, videoAudio = false, livePhotos = false },
            slideshow = new
            {
                intervalSeconds = 10,
                shuffle = true,
                transitionEffect = "fade",
                photoMotion = "none",
                refreshIntervalMinutes = 60,
                preventScreensaver = false
            },
            display = new
            {
                formatSource = "profile",
                overlayStyle = "bottom",
                backgroundEffect = "blur",
                overlayFields = new[] { "date" },
                overlayBehavior = "fade",
                overlayFadeSeconds = 5,
                clockAlwaysVisible = true,
                clockFormat = "HH:mm",
                weatherUnit = "celsius",
                showTimer = true,
                showDate = true,
                dateFormat = "d MMMM yyyy",
                locale = "en-US"
            },
            imageQuality = "preview",
            dateFilter = (object?)null,
            weather = (object?)null
        });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task PostProfile_InvalidIntervalSeconds_Returns400()
    {
        var body = new
        {
            id = "bad-interval", name = "Bad",
            contentSources = Array.Empty<object>(),
            mediaTypes = new { photos = true, videos = false, videoAudio = false, livePhotos = false },
            slideshow = new { intervalSeconds = 1, shuffle = true, transitionEffect = "fade", refreshIntervalMinutes = 60 },
            display = new { formatSource = "roku", overlayStyle = "none", backgroundEffect = "blur", overlayFields = Array.Empty<string>(), overlayBehavior = "manual", overlayFadeSeconds = 5, clockAlwaysVisible = false, clockFormat = "HH:mm", weatherUnit = "celsius", showTimer = true, showDate = true, dateFormat = "d MMMM yyyy", locale = "en-US" },
            imageQuality = "preview"
        };
        Assert.AreEqual(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/profiles", body)).StatusCode);
    }

    [TestMethod]
    public async Task PostProfile_InvalidImageQuality_Returns400()
    {
        var body = new
        {
            id = "bad-quality", name = "Bad",
            contentSources = Array.Empty<object>(),
            mediaTypes = new { photos = true, videos = false, videoAudio = false, livePhotos = false },
            slideshow = new { intervalSeconds = 10, shuffle = true, transitionEffect = "fade", refreshIntervalMinutes = 60 },
            display = new { formatSource = "roku", overlayStyle = "none", backgroundEffect = "blur", overlayFields = Array.Empty<string>(), overlayBehavior = "manual", overlayFadeSeconds = 5, clockAlwaysVisible = false, clockFormat = "HH:mm", weatherUnit = "celsius", showTimer = true, showDate = true, dateFormat = "d MMMM yyyy", locale = "en-US" },
            imageQuality = "ultra"
        };
        Assert.AreEqual(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/profiles", body)).StatusCode);
    }
}
