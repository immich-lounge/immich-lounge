using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using ImmichLoungeCompanion.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImmichLoungeCompanion.Tests.Api;

[TestClass]
public class ClockApiTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public ClockApiTests() => _client = _factory.CreateClient();
    public void Dispose() => _factory.Dispose();

    [TestMethod]
    public async Task GetClock_ReturnsRawIsoDate()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/profiles", new
        {
            id = "clock-de",
            name = "Clock DE",
            description = "German clock format",
            contentSources = Array.Empty<object>(),
            mediaTypes = new { photos = true, videos = false, videoAudio = false, livePhotos = false },
            slideshow = new { intervalSeconds = 10, shuffle = true, transitionEffect = "fade", refreshIntervalMinutes = 60 },
            display = new
            {
                formatSource = "roku",
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
                locale = "de-DE"
            },
            imageQuality = "preview"
        });

        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

        var response = await _client.GetAsync("/api/profiles/clock-de/clock");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var actual = body.GetProperty("dateIso").GetString();
        var expected = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");

        Assert.AreEqual(expected, actual);
        Assert.IsTrue(body.TryGetProperty("formattedDate", out var formattedDateElement));
        Assert.AreEqual(JsonValueKind.Null, formattedDateElement.ValueKind);
    }

    [TestMethod]
    public async Task GetClock_ReturnsFormattedDate_WhenProfileFormattingIsSelected()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/profiles", new
        {
            id = "clock-custom",
            name = "Clock Custom",
            description = "Custom date format",
            contentSources = Array.Empty<object>(),
            mediaTypes = new { photos = true, videos = false, videoAudio = false, livePhotos = false },
            slideshow = new { intervalSeconds = 10, shuffle = true, transitionEffect = "fade", refreshIntervalMinutes = 60 },
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
                locale = "de-DE"
            },
            imageQuality = "preview"
        });

        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

        var response = await _client.GetAsync("/api/profiles/clock-custom/clock");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var actualIso = body.GetProperty("dateIso").GetString();
        var actualFormatted = body.GetProperty("formattedDate").GetString();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var expectedIso = today.ToString("yyyy-MM-dd");
        var expectedFormatted = $"{today.Day}. {today.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("de-DE"))}";

        Assert.AreEqual(expectedIso, actualIso);
        Assert.AreEqual(expectedFormatted, actualFormatted);
    }
}
