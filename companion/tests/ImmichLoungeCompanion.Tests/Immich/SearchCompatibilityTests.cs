using System.Net;
using System.Text;
using System.Text.Json;
using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImmichLoungeCompanion.Tests.Immich;

[TestClass]
public class SearchCompatibilityTests
{
    private static ImmichSettings Settings(string url = "http://immich.example") => new() { ServerUrl = url, ApiKey = "key" };
    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
    private static HttpResponseMessage Version(int minor) => Json($$"""{"major":3,"minor":{{minor}},"patch":0,"prerelease":null}""");
    private static HttpResponseMessage EmptyPage() => Json("""{"assets":{"items":[],"nextPage":null,"nextCursor":null}}""");

    [TestMethod]
    [DataRow(2, 6, false)]
    [DataRow(3, 1, false)]
    [DataRow(3, 2, true)]
    [DataRow(3, 3, true)]
    [DataRow(4, 0, true)]
    public async Task DetectsVersionAndSelectsWireFormat(int major, int minor, bool structured)
    {
        JsonElement sent = default;
        var client = Client(async req =>
        {
            if (req.Method == HttpMethod.Get)
            {
                Assert.AreEqual("/api/server/version", req.RequestUri!.AbsolutePath);
                Assert.IsFalse(req.Headers.Contains("x-api-key"));
                return Json($$"""{"major":{{major}},"minor":{{minor}},"patch":0,"prerelease":null}""");
            }
            sent = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()).RootElement.Clone();
            Assert.AreEqual("/api/search/metadata", req.RequestUri!.AbsolutePath);
            Assert.AreEqual("key", req.Headers.GetValues("x-api-key").Single());
            return EmptyPage();
        });
        await client.SearchAssetsAllPagesAsync(Settings(), new());
        Assert.AreEqual(structured, sent.TryGetProperty("filter", out _));
        Assert.AreEqual(!structured, sent.TryGetProperty("page", out _));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task SearchReadsFavoriteAndRatingMetadata(int minor)
    {
        var client = Client(req => Task.FromResult(req.Method == HttpMethod.Get ? Version(minor) :
            Json("""{"assets":{"items":[{"id":"rated","isFavorite":true,"exifInfo":{"rating":4}},{"id":"unrated","isFavorite":false,"exifInfo":{"rating":null}}],"nextPage":null,"nextCursor":null}}""")));
        var assets = await client.SearchAssetsAllPagesAsync(Settings(), new());
        Assert.AreEqual(true, assets[0].IsFavorite);
        Assert.AreEqual(4, assets[0].ExifInfo!.Rating);
        Assert.AreEqual(false, assets[1].IsFavorite);
        Assert.IsNull(assets[1].ExifInfo!.Rating);
    }

    [TestMethod]
    public async Task V2PreservesAllFiltersAndUsesCursorWithoutMutatingRequest()
    {
        var bodies = new List<JsonElement>();
        var client = Client(async req =>
        {
            if (req.Method == HttpMethod.Get) { return Version(2); }
            var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()).RootElement.Clone();
            bodies.Add(body);
            return bodies.Count == 1
                ? Json("""{"assets":{"items":[{"id":"first"}],"nextCursor":"opaque+/=","nextPage":null}}""")
                : Json("""{"assets":{"items":[{"id":"second"}],"nextCursor":null,"nextPage":null}}""");
        });
        var request = new SearchAssetsRequest
        {
            AlbumIds = ["album1", "album2"], PersonIds = ["person"], TagIds = ["tag"],
            Country = "Switzerland", State = "Zürich", City = "Zürich", Type = "IMAGE",
            TakenAfter = "2020-01-01", TakenBefore = "2021-01-01", HasPeople = true, Page = 7, Size = 12
        };
        var assets = await client.SearchAssetsAllPagesAsync(Settings(), request);
        CollectionAssert.AreEqual(new[] { "first", "second" }, assets.Select(a => a.Id).ToArray());
        Assert.AreEqual(7, request.Page);
        Assert.AreEqual("2020-01-01", request.TakenAfter);
        Assert.IsFalse(bodies[0].TryGetProperty("cursor", out _));
        Assert.AreEqual("opaque+/=", bodies[1].GetProperty("cursor").GetString());
        var filter = bodies[0].GetProperty("filter");
        CollectionAssert.AreEqual(new[] { "hidden", "locked" }, filter.GetProperty("visibility").GetProperty("notIn")
            .EnumerateArray().Select(v => v.GetString()).ToArray());
        Assert.AreEqual(2, filter.GetProperty("albumIds").GetProperty("all").GetArrayLength());
        Assert.AreEqual("person", filter.GetProperty("personIds").GetProperty("all")[0].GetString());
        Assert.AreEqual("tag", filter.GetProperty("tagIds").GetProperty("all")[0].GetString());
        Assert.AreEqual("Switzerland", filter.GetProperty("country").GetProperty("eq").GetString());
        Assert.AreEqual("Zürich", filter.GetProperty("state").GetProperty("eq").GetString());
        Assert.AreEqual("Zürich", filter.GetProperty("city").GetProperty("eq").GetString());
        Assert.AreEqual("IMAGE", filter.GetProperty("type").GetProperty("eq").GetString());
        Assert.AreEqual("2020-01-01T00:00:00.000Z", filter.GetProperty("takenAt").GetProperty("gte").GetString());
        Assert.AreEqual("2021-01-01T23:59:59.999Z", filter.GetProperty("takenAt").GetProperty("lte").GetString());
        Assert.AreEqual(12, bodies[0].GetProperty("size").GetInt32());
        Assert.IsTrue(bodies[0].GetProperty("withExif").GetBoolean());
        foreach (var body in bodies)
        {
            Assert.IsTrue(body.GetProperty("filter").GetProperty("hasPeople").GetProperty("eq").GetBoolean());
            Assert.IsFalse(body.TryGetProperty("withPeople", out _));
        }
        foreach (var name in new[] { "page", "takenAfter", "takenBefore", "country", "state", "city", "albumIds", "personIds", "tagIds", "type" })
        {
            Assert.IsFalse(bodies[0].TryGetProperty(name, out _), name);
        }
    }

    [TestMethod]
    public async Task LegacyPreservesFiltersAndReadsAllNumberedPages()
    {
        var bodies = new List<JsonElement>();
        var client = Client(async req =>
        {
            if (req.Method == HttpMethod.Get) { return Version(1); }
            bodies.Add(JsonDocument.Parse(await req.Content!.ReadAsStringAsync()).RootElement.Clone());
            return bodies.Count == 1
                ? Json("""{"assets":{"items":[{"id":"first"}],"nextPage":"2"}}""")
                : Json("""{"assets":{"items":[{"id":"second"}],"nextPage":null}}""");
        });
        var assets = await client.SearchAssetsAllPagesAsync(Settings(), new()
        {
            AlbumIds = ["a"], PersonIds = ["p"], TagIds = ["t"], Type = "IMAGE",
            Country = "Canada", State = "British Columbia", City = "Vancouver", TakenAfter = "2020-01-01"
        });
        Assert.AreEqual(2, assets.Count);
        Assert.AreEqual(1, bodies[0].GetProperty("page").GetInt32());
        Assert.AreEqual(2, bodies[1].GetProperty("page").GetInt32());
        Assert.AreEqual("a", bodies[0].GetProperty("albumIds")[0].GetString());
        Assert.AreEqual("p", bodies[0].GetProperty("personIds")[0].GetString());
        Assert.AreEqual("t", bodies[0].GetProperty("tagIds")[0].GetString());
        Assert.AreEqual("IMAGE", bodies[0].GetProperty("type").GetString());
        Assert.AreEqual("Canada", bodies[0].GetProperty("country").GetString());
        Assert.AreEqual("British Columbia", bodies[0].GetProperty("state").GetString());
        Assert.AreEqual("Vancouver", bodies[0].GetProperty("city").GetString());
        Assert.AreEqual("2020-01-01T00:00:00.000Z", bodies[0].GetProperty("takenAfter").GetString());
        Assert.IsFalse(bodies[0].TryGetProperty("filter", out _));
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("invalid-json")]
    [DataRow("missing-fields")]
    [DataRow("negative")]
    [DataRow("timeout")]
    public async Task FailedDetectionFallsBackWithoutLosingLocation(string failure)
    {
        var client = Client(async req =>
        {
            if (req.Method == HttpMethod.Get)
            {
                return failure switch
                {
                    "missing" => new(HttpStatusCode.NotFound),
                    "invalid-json" => Json("not-json"),
                    "missing-fields" => Json("{}"),
                    "negative" => Json("""{"major":-1,"minor":2,"patch":0}"""),
                    _ => throw new TaskCanceledException()
                };
            }
            var sent = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()).RootElement;
            Assert.AreEqual("Canada", sent.GetProperty("country").GetString());
            Assert.AreEqual(1, sent.GetProperty("page").GetInt32());
            return EmptyPage();
        });
        await client.SearchAssetsAllPagesAsync(Settings(), new() { Country = "Canada" });
        var capabilities = await client.GetSearchApiCapabilitiesAsync(Settings());
        Assert.IsFalse(capabilities.StructuredSearch);
        Assert.IsNotNull(capabilities.Warning);
    }

    [TestMethod]
    public async Task DetectionRefreshesAfterExpiryAndServerChange()
    {
        var time = new TestTimeProvider();
        var minor = 1;
        var probes = 0;
        var client = Client(req =>
        {
            probes++;
            return Task.FromResult(Version(minor));
        }, time);
        Assert.IsFalse((await client.GetSearchApiCapabilitiesAsync(Settings())).StructuredSearch);
        minor = 2;
        Assert.IsFalse((await client.GetSearchApiCapabilitiesAsync(Settings("http://immich.example/"))).StructuredSearch);
        Assert.AreEqual(1, probes);
        time.Advance(TimeSpan.FromMinutes(6));
        Assert.IsTrue((await client.GetSearchApiCapabilitiesAsync(Settings())).StructuredSearch);
        minor = 1; // downgrade
        Assert.IsFalse((await client.GetSearchApiCapabilitiesAsync(Settings(), refresh: true)).StructuredSearch);
        minor = 2;
        Assert.IsTrue((await client.GetSearchApiCapabilitiesAsync(Settings("http://other.example"))).StructuredSearch);
        Assert.AreEqual(4, probes);
    }

    [TestMethod]
    public async Task FailedDetectionIsRetriedAfterShortExpiry()
    {
        var time = new TestTimeProvider();
        var probes = 0;
        var client = Client(req => Task.FromResult(++probes == 1 ? new HttpResponseMessage(HttpStatusCode.NotFound) : Version(2)), time);
        Assert.IsFalse((await client.GetSearchApiCapabilitiesAsync(Settings())).StructuredSearch);
        time.Advance(TimeSpan.FromSeconds(31));
        Assert.IsTrue((await client.GetSearchApiCapabilitiesAsync(Settings())).StructuredSearch);
    }

    [TestMethod]
    public async Task ConcurrentDetectionUsesOneProbe()
    {
        var probes = 0;
        var client = Client(async req =>
        {
            Interlocked.Increment(ref probes);
            await Task.Yield();
            return Version(2);
        });
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => client.GetSearchApiCapabilitiesAsync(Settings())));
        Assert.AreEqual(1, probes);
    }

    [TestMethod]
    public async Task PrereleaseAtFeatureBoundaryUsesLegacy()
    {
        var client = Client(req => Task.FromResult(Json("""{"major":3,"minor":2,"patch":0,"prerelease":1}""")));
        Assert.IsFalse((await client.GetSearchApiCapabilitiesAsync(Settings())).StructuredSearch);
    }

    [TestMethod]
    [DataRow(400)]
    [DataRow(401)]
    [DataRow(403)]
    [DataRow(500)]
    public async Task SearchErrorsNeverDowngradeOrDropFilters(int status)
    {
        var searches = 0;
        var client = Client(req =>
        {
            if (req.Method == HttpMethod.Get) { return Task.FromResult(Version(2)); }
            searches++;
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status));
        });
        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => client.SearchAssetsAllPagesAsync(Settings(), new() { Country = "Canada" }));
        Assert.AreEqual(1, searches);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RepeatedPaginationTokenFailsInsteadOfLooping(bool structured)
    {
        var client = Client(req => Task.FromResult(req.Method == HttpMethod.Get ? Version(structured ? 2 : 1)
            : structured ? Json("""{"assets":{"items":[],"nextCursor":"same"}}""")
            : Json("""{"assets":{"items":[],"nextPage":"1"}}""")));
        await Assert.ThrowsExactlyAsync<JsonException>(() => client.SearchAssetsAllPagesAsync(Settings(), new()));
    }

    [TestMethod]
    public async Task SuggestionsEncodeLocationNames()
    {
        var client = Client(req =>
        {
            Assert.AreEqual("/api/search/suggestions?type=city&country=Bosnia%20%26%20Herzegovina&state=A%2FB",
                req.RequestUri!.PathAndQuery);
            Assert.AreEqual("key", req.Headers.GetValues("x-api-key").Single());
            return Task.FromResult(Json("""["Sarajevo"]"""));
        });
        var names = await client.GetLocationSuggestionsAsync(Settings(), "city", "Bosnia & Herzegovina", "A/B");
        Assert.AreEqual("Sarajevo", names.Single());
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"assets\":null}")]
    [DataRow("{\"assets\":{\"items\":null,\"nextCursor\":null}}")]
    [DataRow("{\"assets\":{\"items\":[],\"nextPage\":null}}")]
    [DataRow("{\"assets\":{\"items\":[],\"nextCursor\":null,\"nextPage\":\"2\"}}")]
    public async Task IncompatibleV2ResponseIsRejectedAndDetectionRefreshed(string response)
    {
        var probes = 0;
        var searches = 0;
        var client = Client(req =>
        {
            if (req.Method == HttpMethod.Get)
            {
                probes++;
                return Task.FromResult(Version(probes == 1 ? 2 : 1));
            }
            searches++;
            return Task.FromResult(Json(response));
        });
        await Assert.ThrowsExactlyAsync<JsonException>(() => client.SearchAssetsAllPagesAsync(Settings(), new() { AlbumIds = ["a"] }));
        Assert.AreEqual(1, searches);
        Assert.AreEqual(2, probes);
        Assert.IsFalse((await client.GetSearchApiCapabilitiesAsync(Settings())).StructuredSearch);
    }

    [TestMethod]
    [DataRow(1, 1, 0)]
    [DataRow(1, 1, 3)]
    [DataRow(1, 1000, 999)]
    [DataRow(1, 1000, 1000)]
    [DataRow(1, 1000, 1001)]
    [DataRow(2, 1, 3)]
    [DataRow(2, 1000, 1000)]
    [DataRow(2, 1000, 1001)]
    [DataRow(3, 1, 0)]
    [DataRow(3, 2, 3)]
    [DataRow(3, 1000, 999)]
    [DataRow(3, 1000, 1000)]
    [DataRow(3, 1000, 1001)]
    [DataRow(3, 1000, 2000)]
    public async Task AlbumPersonSearchPreservesScopeAcrossPageSizeBoundaries(int minor, int size, int total)
    {
        var calls = 0;
        var client = Client(async req =>
        {
            if (req.Method == HttpMethod.Get) { return Version(minor); }
            var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()).RootElement;
            var offset = calls++ * size;
            Assert.AreEqual(size, body.GetProperty("size").GetInt32());
            if (minor >= 2)
            {
                var filter = body.GetProperty("filter");
                Assert.AreEqual("album", filter.GetProperty("albumIds").GetProperty("all")[0].GetString());
                Assert.AreEqual("person", filter.GetProperty("personIds").GetProperty("all")[0].GetString());
                if (offset > 0) { Assert.AreEqual(offset.ToString(), body.GetProperty("cursor").GetString()); }
            }
            else
            {
                Assert.AreEqual(calls, body.GetProperty("page").GetInt32());
                Assert.AreEqual("album", body.GetProperty("albumIds")[0].GetString());
                Assert.AreEqual("person", body.GetProperty("personIds")[0].GetString());
            }
            var count = Math.Min(size, total - offset);
            var items = Enumerable.Range(offset, count).Select(i => new { id = i.ToString() }).ToArray();
            var more = offset + count < total;
            return Json(JsonSerializer.Serialize(new { assets = new { items,
                nextCursor = minor >= 2 && more ? (offset + count).ToString() : null,
                nextPage = minor == 1 && more ? (calls + 1).ToString() : null } }));
        });
        var result = await client.SearchAssetsAllPagesAsync(Settings(), new() { AlbumIds = ["album"], PersonIds = ["person"], Size = size });
        CollectionAssert.AreEqual(Enumerable.Range(0, total).Select(i => i.ToString()).ToArray(), result.Select(a => a.Id).ToArray());
        Assert.AreEqual(Math.Max(1, (total + size - 1) / size), calls, "A full final page must stop when its continuation token is null.");
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(3)]
    public async Task AlbumPersonSearchFailureOnLaterPageThrowsInsteadOfServingFirstPage(int minor)
    {
        var calls = 0;
        var client = Client(req => Task.FromResult(req.Method == HttpMethod.Get ? Version(minor)
            : ++calls == 1 ? Json(minor == 1
                ? """{"assets":{"items":[{"id":"first"}],"nextPage":"2"}}"""
                : """{"assets":{"items":[{"id":"first"}],"nextCursor":"next","nextPage":null}}""")
            : new HttpResponseMessage(HttpStatusCode.Forbidden)));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAssetsAllPagesAsync(Settings(), new() { AlbumIds = ["album"], PersonIds = ["person"], Size = 1 }));
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(-1)]
    public async Task PeoplePresenceNeverDropsFilterOnLegacyOrUnknownServers(int minor)
    {
        var searches = 0;
        var client = Client(req =>
        {
            if (req.Method == HttpMethod.Get)
            {
                return Task.FromResult(minor < 0 ? new HttpResponseMessage(HttpStatusCode.NotFound) : Version(minor));
            }
            searches++;
            return Task.FromResult(EmptyPage());
        });
        var error = await Assert.ThrowsExactlyAsync<NotSupportedException>(() =>
            client.SearchAssetsAllPagesAsync(Settings(), new() { HasPeople = true }));
        StringAssert.Contains(error.Message, "Immich 3.2");
        Assert.AreEqual(0, searches);
    }

    [TestMethod]
    public async Task DefaultStructuredSearchDoesNotFilterByPeoplePresence()
    {
        var client = Client(async req =>
        {
            if (req.Method == HttpMethod.Get) { return Version(2); }
            var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()).RootElement;
            Assert.IsFalse(body.GetProperty("filter").TryGetProperty("hasPeople", out _));
            return EmptyPage();
        });
        await client.SearchAssetsAllPagesAsync(Settings(), new());
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public async Task DateOnlyEndIncludesWholeDayAndExplicitTimestampsStayExact(int minor)
    {
        JsonElement sent = default;
        var client = Client(async req =>
        {
            if (req.Method == HttpMethod.Get) { return Version(minor); }
            sent = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()).RootElement.Clone();
            return EmptyPage();
        });
        foreach (var value in new[] { "2024-02-29", "2024-02-29T13:14:15+02:00" })
        {
            var request = new SearchAssetsRequest { TakenBefore = value };
            await client.SearchAssetsAllPagesAsync(Settings(), request);
            var actual = minor == 1 ? sent.GetProperty("takenBefore").GetString()
                : sent.GetProperty("filter").GetProperty("takenAt").GetProperty("lte").GetString();
            Assert.AreEqual(value.Contains('T') ? value : "2024-02-29T23:59:59.999Z", actual);
            Assert.AreEqual(value, request.TakenBefore);
        }
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public async Task EmptyDateBoundsAreNotSentToImmich(int minor)
    {
        var client = Client(async req =>
        {
            if (req.Method == HttpMethod.Get) { return Version(minor); }
            var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()).RootElement;
            if (minor == 1)
            {
                Assert.IsFalse(body.TryGetProperty("takenAfter", out _));
                Assert.IsFalse(body.TryGetProperty("takenBefore", out _));
            }
            else { Assert.IsFalse(body.GetProperty("filter").TryGetProperty("takenAt", out _)); }
            return EmptyPage();
        });
        await client.SearchAssetsAllPagesAsync(Settings(), new() { TakenAfter = " ", TakenBefore = "" });
    }

    [TestMethod]
    public async Task NoFacePredicateIsSerializedAsExplicitFalse()
    {
        var client = Client(async req =>
        {
            if (req.Method == HttpMethod.Get) { return Version(2); }
            var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()).RootElement;
            Assert.IsFalse(body.GetProperty("filter").GetProperty("hasPeople").GetProperty("eq").GetBoolean());
            Assert.IsFalse(body.TryGetProperty("hasNoPeople", out _));
            return EmptyPage();
        });
        await client.SearchAssetsAllPagesAsync(Settings(), new() { HasNoPeople = true });
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(-1)]
    public async Task NoFaceNeverDropsPredicateOnLegacyOrUnknownServer(int minor)
    {
        var searches = 0;
        var client = Client(req =>
        {
            if (req.Method == HttpMethod.Get) { return Task.FromResult(minor < 0 ? new HttpResponseMessage(HttpStatusCode.NotFound) : Version(minor)); }
            searches++; return Task.FromResult(EmptyPage());
        });
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => client.SearchAssetsAllPagesAsync(Settings(), new() { HasNoPeople = true }));
        Assert.AreEqual(0, searches);
    }

    private static ImmichClient Client(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond, TimeProvider? time = null) =>
        new(new Factory(new HttpClient(new Handler(respond))), NullLogger<ImmichClient>.Instance, time);

    private sealed class Factory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.Parse("2026-10-10T12:00:00Z");
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
