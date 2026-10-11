using System.Net;
using ImmichLoungeCompanion.Tests.Helpers;
using ImmichLoungeCompanion.Components.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ImmichLoungeCompanion.Tests.App;

[TestClass]
public sealed class BlazorShellTests
{
    private readonly TestWebApplicationFactory _factory = new();

    [TestMethod]
    [DataRow("/profiles")]
    [DataRow("/schedules")]
    [DataRow("/connection")]
    public async Task Navigation_RendersCompanionVersion(string path)
    {
        using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<NavigationManager>(new TestNavigationManager(path))
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<NavMenu>()).ToHtmlString());
        html = System.Text.RegularExpressions.Regex.Replace(html, "<!--.*?-->", "");

        Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(html,
            "<div class=\"nav-version\"[^>]*>Companion v[0-9]+\\.[0-9]+\\.[0-9]+(?:-[^<]+)?</div>"),
            "Navigation should render a release version, not literal Razor source: " + html);
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager(string path) => Initialize("http://localhost/", "http://localhost" + path);
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }

    [TestMethod]
    public async Task BlazorFrameworkScript_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/_framework/blazor.web.js");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
