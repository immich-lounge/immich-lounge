using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ImmichLoungeCompanion.Api;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using ImmichLoungeCompanion.Services;
using ImmichLoungeCompanion.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.Api;

[TestClass]
public class SelectionPreviewTests
{
    [TestMethod]
    public async Task Preview_ReturnsSixInlineSamplesAndFullCount_WithoutCredentials()
    {
        using var handler = new ThumbnailHandler("image/jpeg", 12, HttpStatusCode.OK);
        var result = await PreviewAsync(handler);
        Assert.AreEqual(8, result.GetProperty("matchedAssets").GetInt32());
        var samples = result.GetProperty("previewAssets");
        Assert.AreEqual(6, samples.GetArrayLength());
        Assert.AreEqual(6, handler.Requests);
        Assert.IsTrue(handler.AllAuthenticated);
        Assert.IsTrue(samples[0].GetProperty("thumbnailUrl").GetString()!.StartsWith("data:image/jpeg;base64,"));
        Assert.IsFalse(result.GetRawText().Contains("preview-secret"));
    }

    [TestMethod]
    [DataRow("text/html", 12, 200)]
    [DataRow("image/jpeg", 524289, 200)]
    [DataRow("image/jpeg", 12, 503)]
    public async Task UnusableThumbnail_DoesNotBreakSelectionCount(string mime, int length, int status)
    {
        using var handler = new ThumbnailHandler(mime, length, (HttpStatusCode)status);
        var result = await PreviewAsync(handler);
        Assert.AreEqual(8, result.GetProperty("matchedAssets").GetInt32());
        Assert.AreEqual(JsonValueKind.Null, result.GetProperty("previewAssets")[0].GetProperty("thumbnailUrl").ValueKind);
    }

    private static async Task<JsonElement> PreviewAsync(ThumbnailHandler handler)
    {
        var builder = Substitute.For<IPlaylistBuilder>();
        builder.BuildAsync(Arg.Any<Profile>(), Arg.Any<CancellationToken>()).Returns(
            Enumerable.Range(0, 8).Select(_ => new PlaylistEntry(Guid.NewGuid().ToString(), "photo", null, null)).ToList());
        var settings = Substitute.For<ISettingsRepository>();
        settings.LoadAsync().Returns(new GlobalSettings { Immich = new() { ServerUrl = "http://immich.test", ApiKey = "preview-secret" } });
        using var http = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("immich").Returns(http);
        var validator = Substitute.For<IProfileValidator>();
        validator.Validate(Arg.Any<Profile>()).Returns((string?)null);
        var controller = new ProfilesController(Substitute.For<IProfileRepository>(), Substitute.For<IPlaylistCache>(), builder,
            validator, Substitute.For<IProfileDocumentService>(), settings, factory);
        var result = (OkObjectResult)await controller.PreviewCount(new Profile(), CancellationToken.None);
        return JsonSerializer.SerializeToElement(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private sealed class ThumbnailHandler(string mime, int length, HttpStatusCode status) : HttpMessageHandler
    {
        public int Requests;
        public bool AllAuthenticated = true;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Requests);
            AllAuthenticated &= request.Headers.GetValues("x-api-key").Single() == "preview-secret";
            var content = new ByteArrayContent(new byte[length]);
            content.Headers.ContentType = new MediaTypeHeaderValue(mime);
            return Task.FromResult(new HttpResponseMessage(status) { Content = content });
        }
    }
}
