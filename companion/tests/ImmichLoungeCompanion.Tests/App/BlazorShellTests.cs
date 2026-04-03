using System.Net;
using ImmichLoungeCompanion.Tests.Helpers;

namespace ImmichLoungeCompanion.Tests.App;

[TestClass]
public sealed class BlazorShellTests
{
    private readonly TestWebApplicationFactory _factory = new();

    [TestMethod]
    public async Task ConnectionRoute_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/connection");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task BlazorFrameworkScript_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/_framework/blazor.web.js");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
