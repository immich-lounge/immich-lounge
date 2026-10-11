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

    [TestMethod]
    [DataRow("cityCountry")]
    [DataRow("cityStateCountry")]
    [DataRow("city")]
    [DataRow("stateCountry")]
    [DataRow("country")]
    public async Task LocationFormat_RoundTripsAndLeavesLocationFilterUnchanged(string format)
    {
        var profile = new Profile { Id = "location-label", Name = "Location label",
            Display = new() { LocationFormat = format }, LocationFilter = new() { Country = "Switzerland" } };
        Assert.AreEqual(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/profiles", profile)).StatusCode);
        var saved = await _client.GetFromJsonAsync<Profile>("/api/profiles/location-label");
        Assert.AreEqual(format, saved!.Display.LocationFormat);
        Assert.AreEqual("Switzerland", saved.LocationFilter!.Country);
        saved.Display.LocationFormat = "country";
        Assert.AreEqual(HttpStatusCode.OK, (await _client.PutAsJsonAsync("/api/profiles/location-label", saved)).StatusCode);
        Assert.AreEqual("country", (await _client.GetFromJsonAsync<Profile>("/api/profiles/location-label"))!.Display.LocationFormat);
    }

    [TestMethod]
    [DataRow("unknown")]
    [DataRow("")]
    [DataRow(null)]
    public async Task InvalidLocationFormat_IsRejected(string? format)
    {
        var profile = new Profile { Id = "invalid-location-label", Name = "Invalid", Display = new() { LocationFormat = format! } };
        Assert.AreEqual(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/profiles", profile)).StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/profiles/preview-count", profile)).StatusCode);
    }

    [TestMethod]
    public async Task OldProfileWithoutLocationFormat_DefaultsToCityCountry()
    {
        Assert.AreEqual(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/profiles",
            new { id = "old-location-label", name = "Old", display = new { overlayStyle = "bottom" } })).StatusCode);
        Assert.AreEqual("cityCountry", (await _client.GetFromJsonAsync<Profile>("/api/profiles/old-location-label"))!.Display.LocationFormat);
    }

    [TestMethod]
    [DataRow(0.1, 3, true)]
    [DataRow(5.0, 10, true)]
    [DataRow(0.0, 10, false)]
    [DataRow(5.1, 10, false)]
    [DataRow(3.0, 3, false)]
    [DataRow(4.0, 3, false)]
    public async Task TransitionDuration_ValidatesAndRoundTrips(double duration, int interval, bool valid)
    {
        var profile = new Profile { Id = "duration", Name = "Duration",
            Slideshow = new() { TransitionDurationSeconds = duration, IntervalSeconds = interval } };
        var response = await _client.PostAsJsonAsync("/api/profiles", profile);
        Assert.AreEqual(valid ? HttpStatusCode.Created : HttpStatusCode.BadRequest, response.StatusCode);
        var preview = await _client.PostAsJsonAsync("/api/profiles/preview-count", profile);
        Assert.AreEqual(valid ? HttpStatusCode.OK : HttpStatusCode.BadRequest, preview.StatusCode);
        if (valid)
        {
            Assert.AreEqual(duration, (await _client.GetFromJsonAsync<Profile>("/api/profiles/duration"))!.Slideshow.TransitionDurationSeconds);
        }
    }

    [TestMethod]
    public async Task OldProfileWithoutTransitionDuration_KeepsHalfSecondDefault()
    {
        var response = await _client.PostAsJsonAsync("/api/profiles", new { id = "old-duration", name = "Old", slideshow = new { intervalSeconds = 10 } });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.AreEqual(0.5, (await _client.GetFromJsonAsync<Profile>("/api/profiles/old-duration"))!.Slideshow.TransitionDurationSeconds);
    }

    [TestMethod]
    [DataRow("album", "id")]
    [DataRow("person", " ")]
    public async Task InvalidExcludedPeople_AreRejectedOnCreateAndPreview(string type, string id)
    {
        var profile = new Profile { Id = "invalid-exclusion", Name = "Invalid",
            ExcludedPeople = [new() { Type = type, Id = id }] };
        Assert.AreEqual(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/profiles", profile)).StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/profiles/preview-count", profile)).StatusCode);
    }

    [TestMethod]
    public async Task LocationFilter_RoundTripsThroughCreateGetUpdateAndDisk()
    {
        var profile = new Profile
        {
            Id = "location-profile", Name = "Location",
            LocationFilter = new() { Country = " Canada ", State = "British Columbia", City = "Vancouver" }
        };
        var created = await _client.PostAsJsonAsync("/api/profiles", profile);
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var saved = await _client.GetFromJsonAsync<Profile>("/api/profiles/location-profile");
        Assert.IsNotNull(saved?.LocationFilter);
        Assert.AreEqual("Canada", saved.LocationFilter.Country);
        Assert.AreEqual("British Columbia", saved.LocationFilter.State);
        Assert.AreEqual("Vancouver", saved.LocationFilter.City);
        saved.LocationFilter.City = "Victoria";
        var updated = await _client.PutAsJsonAsync("/api/profiles/location-profile", saved);
        Assert.AreEqual(HttpStatusCode.OK, updated.StatusCode);
        var loaded = await _client.GetFromJsonAsync<Profile>("/api/profiles/location-profile");
        Assert.AreEqual("Victoria", loaded!.LocationFilter!.City);
        loaded.LocationFilter = new() { Country = " " };
        var cleared = await _client.PutAsJsonAsync("/api/profiles/location-profile", loaded);
        Assert.AreEqual(HttpStatusCode.OK, cleared.StatusCode);
        Assert.IsNull((await _client.GetFromJsonAsync<Profile>("/api/profiles/location-profile"))!.LocationFilter);
    }

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
    public async Task CorruptProfile_DoesNotBreakHealthyProfilesOrPlaylistRefresh()
    {
        var created = await _client.PostAsJsonAsync("/api/profiles", NewProfileBody("healthy-profile"));
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var brokenPath = System.IO.Path.Combine(_factory.DataDirectory, "profiles", "broken.json");
        await File.WriteAllTextAsync(brokenPath, "{");

        var response = await _client.GetAsync("/api/profiles");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("healthy-profile", list.EnumerateArray().Single().GetProperty("id").GetString());
        Assert.AreEqual(HttpStatusCode.NotFound, (await _client.GetAsync("/api/profiles/broken")).StatusCode);

        var worker = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<ImmichLoungeCompanion.Playlist.PlaylistCacheWorker>(_factory.Services);
        await (Task)typeof(ImmichLoungeCompanion.Playlist.PlaylistCacheWorker)
            .GetMethod("RebuildExpiredCachesAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(worker, [CancellationToken.None])!;
        Assert.AreEqual(HttpStatusCode.OK, (await _client.GetAsync("/healthz")).StatusCode);
        Assert.AreEqual("{", await File.ReadAllTextAsync(brokenPath));
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
        Assert.IsFalse(body.Display.ZoomToFit);
        Assert.IsFalse(body.Display.SideBySidePortraits);
        Assert.IsNotNull(body.AssetFilter);
        Assert.AreEqual(AssetFilterRuleKind.Group, body.AssetFilter.Kind);
        Assert.AreEqual(AssetFilterGroupOperator.Or, body.AssetFilter.Operator);
    }

    [TestMethod]
    public async Task ZoomToFit_IsSavedServedAndCanBeDisabled()
    {
        var profile = new Profile
        {
            Id = "zoom-fit",
            Name = "Zoom fit",
            Display = new DisplaySettings { ZoomToFit = true }
        };
        var created = await _client.PostAsJsonAsync("/api/profiles", profile);
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var response = await created.Content.ReadFromJsonAsync<Profile>();
        Assert.IsTrue(response!.Display.ZoomToFit);

        var saved = await _client.GetFromJsonAsync<Profile>("/api/profiles/zoom-fit");
        Assert.IsTrue(saved!.Display.ZoomToFit);
        saved.Display.ZoomToFit = false;
        var updated = await _client.PutAsJsonAsync("/api/profiles/zoom-fit", saved);
        Assert.AreEqual(HttpStatusCode.OK, updated.StatusCode);

        var reloaded = await _client.GetFromJsonAsync<Profile>("/api/profiles/zoom-fit");
        Assert.IsFalse(reloaded!.Display.ZoomToFit);
    }

    [TestMethod]
    public async Task SideBySidePortraits_IsSavedServedAndCanBeDisabled()
    {
        var profile = new Profile
        {
            Id = "portrait-pair",
            Name = "Portrait pair",
            Display = new DisplaySettings { SideBySidePortraits = true }
        };
        var created = await _client.PostAsJsonAsync("/api/profiles", profile);
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var saved = await _client.GetFromJsonAsync<Profile>("/api/profiles/portrait-pair");
        Assert.IsTrue(saved!.Display.SideBySidePortraits);
        saved.Display.SideBySidePortraits = false;
        var updated = await _client.PutAsJsonAsync("/api/profiles/portrait-pair", saved);
        Assert.AreEqual(HttpStatusCode.OK, updated.StatusCode);
        var reloaded = await _client.GetFromJsonAsync<Profile>("/api/profiles/portrait-pair");
        Assert.IsFalse(reloaded!.Display.SideBySidePortraits);
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
    public async Task PutProfile_WithFahrenheitWeatherUnit_PersistsWeatherUnit()
    {
        await _client.PostAsJsonAsync("/api/profiles", NewProfileBody("weather-unit"));

        var response = await _client.PutAsJsonAsync("/api/profiles/weather-unit", new
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
                weatherUnit = "fahrenheit",
                showTimer = true,
                showDate = true,
                dateFormat = "d MMMM yyyy",
                locale = "en-US"
            },
            imageQuality = "original",
            dateFilter = (object?)null,
            weather = new
            {
                enabled = true,
                latitude = 40.7128,
                longitude = -74.0060,
                pollIntervalMinutes = 20
            }
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Profile>();
        Assert.IsNotNull(body);
        Assert.AreEqual("fahrenheit", body.Display.WeatherUnit);
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

    [TestMethod]
    public async Task PostProfile_InvalidWeatherUnit_Returns400()
    {
        var body = new
        {
            id = "bad-weather-unit", name = "Bad",
            contentSources = Array.Empty<object>(),
            mediaTypes = new { photos = true, videos = false, videoAudio = false, livePhotos = false },
            slideshow = new { intervalSeconds = 10, shuffle = true, transitionEffect = "fade", refreshIntervalMinutes = 60 },
            display = new { formatSource = "roku", overlayStyle = "none", backgroundEffect = "blur", overlayFields = Array.Empty<string>(), overlayBehavior = "manual", overlayFadeSeconds = 5, clockAlwaysVisible = false, clockFormat = "HH:mm", weatherUnit = "kelvin", showTimer = true, showDate = true, dateFormat = "d MMMM yyyy", locale = "en-US" },
            imageQuality = "preview"
        };
        Assert.AreEqual(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/profiles", body)).StatusCode);
    }
}
