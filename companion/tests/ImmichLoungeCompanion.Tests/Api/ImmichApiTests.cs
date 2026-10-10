using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Tests.Helpers;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.Api;

[TestClass]
public sealed class ImmichApiTests
{
    [TestMethod]
    public async Task TestConnectionIncludesDetectedApiWithoutChangingExistingFields()
    {
        using var factory = new TestWebApplicationFactory();
        factory.ImmichClient.TestConnectionAsync(Arg.Any<ImmichSettings>())
            .Returns((true, 12, 3, (string?)null));
        factory.ImmichClient.GetSearchApiCapabilitiesAsync(Arg.Any<ImmichSettings>(), true)
            .Returns(new SearchApiCapabilities("3.2.0", true));
        using var client = factory.CreateClient();
        var result = await client.GetFromJsonAsync<JsonElement>("/api/immich/test");
        Assert.IsTrue(result.GetProperty("ok").GetBoolean());
        Assert.AreEqual(12, result.GetProperty("imageCount").GetInt32());
        Assert.AreEqual(3, result.GetProperty("videoCount").GetInt32());
        Assert.AreEqual("3.2.0", result.GetProperty("searchApi").GetProperty("serverVersion").GetString());
        Assert.IsTrue(result.GetProperty("searchApi").GetProperty("structuredSearch").GetBoolean());
    }

    [TestMethod]
    public async Task FailedConnectionDoesNotRequireVersionDetection()
    {
        using var factory = new TestWebApplicationFactory();
        factory.ImmichClient.TestConnectionAsync(Arg.Any<ImmichSettings>())
            .Returns((false, 0, 0, "HTTP 401"));
        using var client = factory.CreateClient();
        var result = await client.GetFromJsonAsync<JsonElement>("/api/immich/test");
        Assert.IsFalse(result.GetProperty("ok").GetBoolean());
        Assert.AreEqual("HTTP 401", result.GetProperty("error").GetString());
        await factory.ImmichClient.DidNotReceive().GetSearchApiCapabilitiesAsync(Arg.Any<ImmichSettings>(), Arg.Any<bool>());
    }

    [TestMethod]
    public async Task LocationSuggestionsForwardScopeAndRejectUnknownTypes()
    {
        using var factory = new TestWebApplicationFactory();
        factory.ImmichClient.GetLocationSuggestionsAsync(Arg.Any<ImmichSettings>(), "city", "Canada", "Ontario")
            .Returns(new List<string> { "Ottawa" });
        using var client = factory.CreateClient();
        var names = await client.GetFromJsonAsync<List<string>>("/api/immich/locations?type=city&country=Canada&state=Ontario");
        Assert.AreEqual("Ottawa", names!.Single());
        var rejected = await client.GetAsync("/api/immich/locations?type=invalid");
        Assert.AreEqual(HttpStatusCode.BadRequest, rejected.StatusCode);
        await factory.ImmichClient.DidNotReceive().GetLocationSuggestionsAsync(
            Arg.Any<ImmichSettings>(), "invalid", Arg.Any<string?>(), Arg.Any<string?>());
    }
}