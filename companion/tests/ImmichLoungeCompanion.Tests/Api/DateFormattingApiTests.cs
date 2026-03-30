using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ImmichLoungeCompanion.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImmichLoungeCompanion.Tests.Api;

[TestClass]
public class DateFormattingApiTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public DateFormattingApiTests() => _client = _factory.CreateClient();
    public void Dispose() => _factory.Dispose();

    [TestMethod]
    public async Task FormatDate_ReturnsProfileFormattedDate()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/profiles", new
        {
            id = "date-format-de",
            name = "Date Format DE",
            description = "German profile formatting",
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

        var response = await _client.PostAsJsonAsync("/api/profiles/date-format-de/format-date", new
        {
            value = "2026-03-24T15:45:12.000Z"
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("24. März 2026", body.GetProperty("formattedDate").GetString());
    }

    [TestMethod]
    public async Task FormatDate_ReturnsNull_WhenRokuFormattingIsSelected()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/profiles", new
        {
            id = "date-format-roku",
            name = "Date Format Roku",
            description = "Roku formatting",
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

        var response = await _client.PostAsJsonAsync("/api/profiles/date-format-roku/format-date", new
        {
            value = "2026-03-24"
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.IsTrue(body.TryGetProperty("formattedDate", out var formattedDateElement));
        Assert.AreEqual(JsonValueKind.Null, formattedDateElement.ValueKind);
    }
}
