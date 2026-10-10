using System.Net;
using System.Net.Http;
using System.Text;
using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImmichLoungeCompanion.Tests.Immich;

[TestClass]
public class ImmichClientTests
{
    [TestMethod]
    public async Task GetAssetAsync_ReadsMetadataAndSkipsDeletedPhotos()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.AreEqual("test-key", request.Headers.GetValues("x-api-key").Single());
            return request.RequestUri!.AbsolutePath.EndsWith("deleted")
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : JsonResponse("""{"id":"photo","type":"IMAGE","exifInfo":{"country":"Switzerland","fileSizeInByte":2048000}}""");
        });
        using var httpClient = new HttpClient(handler);
        var client = new ImmichClient(new StubHttpClientFactory(httpClient), NullLogger<ImmichClient>.Instance);
        var settings = new ImmichSettings { ServerUrl = "http://immich.example/", ApiKey = "test-key" };
        var photo = await client.GetAssetAsync(settings, "photo");
        Assert.AreEqual("Switzerland", photo!.ExifInfo!.Country);
        Assert.AreEqual(2048000L, photo.ExifInfo.FileSizeInByte);
        Assert.IsNull(await client.GetAssetAsync(settings, "deleted"));
    }

    [TestMethod]
    public async Task GetMemoriesAsync_SendsPlainDateAndReadsMemoryAssets()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.AreEqual(HttpMethod.Get, request.Method);
            Assert.AreEqual("http://immich.example/api/memories?for=2026-09-02", request.RequestUri!.AbsoluteUri);
            Assert.AreEqual("test-key", request.Headers.GetValues("x-api-key").Single());
            return JsonResponse("""
                [{ "id": "memory-1", "type": "birthday", "data": { "year": 2014, "personId": "person-1", "personName": "Emma" }, "assets": [{ "id": "photo-1", "type": "IMAGE" }] }]
                """);
        });
        using var httpClient = new HttpClient(handler);
        var client = new ImmichClient(new StubHttpClientFactory(httpClient), NullLogger<ImmichClient>.Instance);

        var memories = await client.GetMemoriesAsync(new ImmichSettings
        {
            ServerUrl = "http://immich.example/",
            ApiKey = "test-key"
        }, new DateOnly(2026, 9, 2));

        Assert.HasCount(1, memories);
        Assert.AreEqual("memory-1", memories[0].Id);
        Assert.AreEqual("birthday", memories[0].Type);
        Assert.AreEqual("Emma", memories[0].Data!.PersonName);
        Assert.AreEqual("person-1", memories[0].Data!.PersonId);
        Assert.AreEqual(2014, memories[0].Data!.Year);
        Assert.HasCount(1, memories[0].Assets);
        Assert.AreEqual("photo-1", memories[0].Assets[0].Id);
    }

    [TestMethod]
    public async Task GetMemoriesAsync_PropagatesBadRequestInsteadOfReturningNoMemories()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("Invalid date", Encoding.UTF8, "text/plain")
        });
        using var httpClient = new HttpClient(handler);
        var client = new ImmichClient(new StubHttpClientFactory(httpClient), NullLogger<ImmichClient>.Instance);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetMemoriesAsync(new ImmichSettings
        {
            ServerUrl = "http://immich.example",
            ApiKey = "test-key"
        }, new DateOnly(2026, 9, 2)));

        Assert.AreEqual(HttpStatusCode.BadRequest, error.StatusCode);
    }

    [TestMethod]
    public async Task GetAlbumsAsync_ReturnsOwnAlbumsOnlyUntilImmichSupportsSharedAlbumSearch()
    {
        var requests = new List<string>();
        var handler = new StubHttpMessageHandler(request =>
        {
            requests.Add(request.RequestUri!.ToString());

            return request.RequestUri!.PathAndQuery switch
            {
                "/api/albums" => JsonResponse("""
                    [
                      { "id": "own-1", "albumName": "Own Album", "assetCount": 3 },
                      { "id": "own-2", "albumName": "Second Own Album", "assetCount": 5 }
                    ]
                    """),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        });

        var httpClient = new HttpClient(handler);
        var factory = new StubHttpClientFactory(httpClient);
        var client = new ImmichClient(factory, NullLogger<ImmichClient>.Instance);

        var albums = await client.GetAlbumsAsync(new ImmichSettings
        {
            ServerUrl = "http://immich.example",
            ApiKey = "test-key"
        });

        CollectionAssert.AreEqual(
            new[]
            {
                "http://immich.example/api/albums"
            },
            requests);

        Assert.AreEqual(2, albums.Count);
        Assert.AreEqual("own-1", albums[0].Id);
        Assert.AreEqual("own-2", albums[1].Id);
    }

    [TestMethod]
    public async Task GetAlbumsAsync_DoesNotCallSharedAlbumsEndpoint()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.PathAndQuery switch
        {
            "/api/albums" => JsonResponse("""
                [
                  { "id": "own-1", "albumName": "Own Album", "assetCount": 3 }
                ]
                """),
            "/api/albums?shared=true" => throw new InvalidOperationException("Shared albums should not be queried while Immich search cannot use them."),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });

        var httpClient = new HttpClient(handler);
        var factory = new StubHttpClientFactory(httpClient);
        var client = new ImmichClient(factory, NullLogger<ImmichClient>.Instance);

        var albums = await client.GetAlbumsAsync(new ImmichSettings
        {
            ServerUrl = "http://immich.example",
            ApiKey = "test-key"
        });

        Assert.AreEqual(1, albums.Count);
        Assert.AreEqual("own-1", albums[0].Id);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
