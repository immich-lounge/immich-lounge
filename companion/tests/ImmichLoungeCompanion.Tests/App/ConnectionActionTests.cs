using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using ImmichLoungeCompanion.Components.Pages;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.App;

[TestClass]
public sealed class ConnectionActionTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    public async Task FailedImmichTestStillReportsThatSettingsWereSaved()
    {
        using var handler = new ConnectionHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var page = CreatePage(client);
        await TestConnectionAsync(page);

        CollectionAssert.AreEqual(new[] { "PUT /api/settings", "GET /api/immich/test" }, handler.Requests);
        Assert.IsTrue(GetField<bool>(page, "_saved"));
        Assert.IsNotNull(GetField<object?>(page, "_testResult"));
        Assert.IsFalse(GetField<bool>(page, "_busy"));
        Assert.IsFalse(GetField<bool>(page, "_testing"));
    }

    [TestMethod]
    public async Task RejectedSettingsDoNotStartAConnectionTest()
    {
        using var handler = new ConnectionHandler(HttpStatusCode.BadRequest);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var page = CreatePage(client);
        await TestConnectionAsync(page);

        CollectionAssert.AreEqual(new[] { "PUT /api/settings" }, handler.Requests);
        Assert.IsFalse(GetField<bool>(page, "_saved"));
        Assert.IsNull(GetField<object?>(page, "_testResult"));
        Assert.AreEqual("Invalid URL", GetField<string?>(page, "_errorMessage"));
        Assert.IsFalse(GetField<bool>(page, "_busy"));
    }

    [TestMethod]
    public async Task InterruptedTestKeepsSavedStatusAndReenablesActions()
    {
        using var handler = new ConnectionHandler(interruptTest: true);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var page = CreatePage(client);
        await TestConnectionAsync(page);

        Assert.IsTrue(GetField<bool>(page, "_saved"));
        StringAssert.Contains(GetField<string>(page, "_errorMessage"), "Settings saved");
        Assert.IsFalse(GetField<bool>(page, "_busy"));
        Assert.IsFalse(GetField<bool>(page, "_testing"));
    }

    private static Connection CreatePage(HttpClient client)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("local").Returns(client);
        var page = new Connection();
        typeof(Connection).GetProperty("HttpFactory", PrivateInstance)!.SetValue(page, factory);
        return page;
    }

    private static Task TestConnectionAsync(Connection page) =>
        (Task)typeof(Connection).GetMethod("TestConnectionAsync", PrivateInstance)!.Invoke(page, null)!;

    private static T GetField<T>(Connection page, string name) =>
        (T)typeof(Connection).GetField(name, PrivateInstance)!.GetValue(page)!;

    private sealed class ConnectionHandler(HttpStatusCode saveStatus = HttpStatusCode.OK, bool interruptTest = false) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            if (request.Method == HttpMethod.Put)
            {
                return Task.FromResult(new HttpResponseMessage(saveStatus) { Content = JsonContent.Create(new { error = "Invalid URL" }) });
            }
            if (interruptTest)
            {
                throw new HttpRequestException("Connection interrupted");
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { ok = false, imageCount = 0, videoCount = 0, error = "Immich is unavailable" })
            });
        }
    }
}
