using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImmichLoungeCompanion.Tests.Immich;

[TestClass]
public class PeoplePaginationTests
{
    private sealed class Handler(Func<int, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            Assert.AreEqual($"?page={Requests}&size=500", request.RequestUri!.Query);
            Assert.AreEqual("key", request.Headers.GetValues("x-api-key").Single());
            return Task.FromResult(response(Requests));
        }
    }
    private sealed class Factory(HttpClient http) : IHttpClientFactory { public HttpClient CreateClient(string name) => http; }
    private static HttpResponseMessage Page(string[] ids, bool? next = null) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new { people = ids.Select(id => new { id, name = id }).ToArray(), hasNextPage = next, total = 9999 })
    };
    private static ImmichClient Client(HttpClient http) => new(new Factory(http), NullLogger<ImmichClient>.Instance);
    private static ImmichSettings Settings => new() { ServerUrl = "http://immich.example", ApiKey = "key" };

    [TestMethod]
    public async Task FetchesAllPagesAndDeduplicatesSharedPeople()
    {
        var handler = new Handler(page => page == 1 ? Page(["a", "b"], true) : Page(["b", "c"], false));
        using var http = new HttpClient(handler);
        var people = await Client(http).GetPeopleAsync(Settings);
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, people.Select(p => p.Id).ToArray());
        Assert.AreEqual(2, handler.Requests);
    }

    [TestMethod]
    public async Task MissingContinuationFlagUsesPageSizeRatherThanTotalIncludingHiddenPeople()
    {
        var handler = new Handler(page => page == 1 ? Page(Enumerable.Range(0, 500).Select(i => i.ToString()).ToArray()) : Page(["last"]));
        using var http = new HttpClient(handler);
        Assert.HasCount(501, await Client(http).GetPeopleAsync(Settings));
        Assert.AreEqual(2, handler.Requests);
    }

    [TestMethod]
    public async Task RepeatedContinuingPageThrowsInsteadOfLooping()
    {
        var handler = new Handler(_ => Page(["same"], true));
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<JsonException>(() => Client(http).GetPeopleAsync(Settings));
        Assert.AreEqual(2, handler.Requests);
    }

    [TestMethod]
    public async Task LaterPageFailureDoesNotReturnAnIncompletePicker()
    {
        var handler = new Handler(page => page == 1 ? Page(["first"], true) : new(HttpStatusCode.ServiceUnavailable));
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => Client(http).GetPeopleAsync(Settings));
    }
}
