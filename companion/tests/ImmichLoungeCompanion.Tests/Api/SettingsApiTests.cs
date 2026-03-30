using System.Net;
using System.Net.Http.Json;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImmichLoungeCompanion.Tests.Api;

[TestClass]
public class SettingsApiTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public SettingsApiTests() => _client = _factory.CreateClient();
    public void Dispose() => _factory.Dispose();

    [TestMethod]
    public async Task GetSettings_ReturnsDefaultSettings()
    {
        var response = await _client.GetAsync("/api/settings");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var settings = await response.Content.ReadFromJsonAsync<GlobalSettings>();
        Assert.AreEqual("Home", settings!.FriendlyName);
        Assert.IsFalse(string.IsNullOrEmpty(settings.CompanionUuid));
    }

    [TestMethod]
    public async Task PutSettings_UpdatesSettings()
    {
        var updated = new GlobalSettings
        {
            FriendlyName = "Living Room Server",
            Immich = new() { ServerUrl = "http://192.168.1.20:2283", ApiKey = "abc" }
        };

        var putResponse = await _client.PutAsJsonAsync("/api/settings", updated);
        Assert.AreEqual(HttpStatusCode.OK, putResponse.StatusCode);

        var getResponse = await _client.GetAsync("/api/settings");
        var loaded = await getResponse.Content.ReadFromJsonAsync<GlobalSettings>();
        Assert.AreEqual("Living Room Server", loaded!.FriendlyName);
        Assert.AreEqual("abc", loaded.Immich.ApiKey);
    }
}
