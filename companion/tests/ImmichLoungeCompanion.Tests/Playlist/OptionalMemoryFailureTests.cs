using System.Net;
using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.Playlist;

[TestClass]
public class OptionalMemoryFailureTests
{
    [TestMethod]
    [DataRow(503)]
    [DataRow(429)]
    [DataRow(408)]
    public async Task TransientOptionalFailureReturnsRegularPhotosAndLogsWarning(int status)
    {
        var client = Mock(new HttpRequestException("memory unavailable", null, (HttpStatusCode)status));
        var logger = Substitute.For<ILogger<PlaylistAssetCollector>>();
        var result = await new PlaylistAssetCollector(client, logger: logger).CollectAsync(Profile(), new());
        CollectionAssert.AreEqual(new[] { "regular-photo" }, result.Keys.ToArray());
        Assert.IsTrue(logger.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "Log" && call.GetArguments()[0] is LogLevel.Warning));
    }

    [TestMethod]
    [DataRow(400)]
    [DataRow(401)]
    [DataRow(403)]
    public async Task PermissionAndInvalidRequestFailuresRemainErrors(int status)
    {
        var client = Mock(new HttpRequestException("rejected", null, (HttpStatusCode)status));
        await Assert.ThrowsAsync<HttpRequestException>(() => new PlaylistAssetCollector(client).CollectAsync(Profile(), new()));
    }

    [TestMethod]
    public async Task MemoryOnlyFailuresDoNotPublishAnEmptyFallback()
    {
        var client = Mock(new HttpRequestException("unavailable", null, HttpStatusCode.ServiceUnavailable));
        var profile = Profile();
        profile.PhotoSource = "memories";
        await Assert.ThrowsAsync<HttpRequestException>(() => new PlaylistAssetCollector(client).CollectAsync(profile, new()));
    }

    [TestMethod]
    public async Task CallerCancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        var client = Mock(new OperationCanceledException());
        client.GetMemoriesAsync(Arg.Any<ImmichSettings>(), Arg.Any<DateOnly>()).Returns(_ =>
        {
            cancellation.Cancel();
            return Task.FromException<List<ImmichMemory>>(new OperationCanceledException(cancellation.Token));
        });
        await Assert.ThrowsAsync<OperationCanceledException>(() => new PlaylistAssetCollector(client).CollectAsync(Profile(), new(), cancellation.Token));
    }

    [TestMethod]
    public async Task ExclusionsStillApplyAfterOptionalMemoryFailure()
    {
        var client = Mock(new HttpRequestException("unavailable", null, HttpStatusCode.ServiceUnavailable));
        var profile = Profile();
        profile.ExcludedPeople = [new() { Type = "person", Id = "excluded" }];
        var result = await new PlaylistAssetCollector(client).CollectAsync(profile, new());
        Assert.HasCount(0, result);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task NetworkErrorsAndRequestTimeoutsKeepRegularPhotos(bool timeout)
    {
        var client = Mock(timeout ? new OperationCanceledException("request timeout") : new HttpRequestException("network unavailable"));
        var result = await new PlaylistAssetCollector(client).CollectAsync(Profile(), new());
        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task ExclusionFailureIsNotSuppressedByMemoryRecovery()
    {
        var client = Mock(new HttpRequestException("unavailable", null, HttpStatusCode.ServiceUnavailable));
        client.SearchAssetsAllPagesAsync(Arg.Any<ImmichSettings>(), Arg.Is<SearchAssetsRequest>(request => request.PersonIds != null))
            .Returns(Task.FromException<List<ImmichAsset>>(new HttpRequestException("exclusion unavailable", null, HttpStatusCode.ServiceUnavailable)));
        var profile = Profile();
        profile.ExcludedPeople = [new() { Type = "person", Id = "excluded" }];
        await Assert.ThrowsAsync<HttpRequestException>(() => new PlaylistAssetCollector(client).CollectAsync(profile, new()));
    }

    private static Profile Profile() => new() { PhotoSource = "all", ContentSources = [new() { Type = "memories" }] };
    [TestMethod]
    [DataRow(false, 503, true)]
    [DataRow(true, 503, false)]
    [DataRow(false, 403, false)]
    public async Task SavedMemoryFailuresFollowTheSameRecoveryRules(bool memoryOnly, int status, bool recovers)
    {
        var client = Mock(new HttpRequestException("daily memory should not be requested"));
        client.GetSearchApiCapabilitiesAsync(Arg.Any<ImmichSettings>(), Arg.Any<bool>())
            .Returns(new SearchApiCapabilities("3.3.1", true));
        client.GetSavedMemoriesAsync(Arg.Any<ImmichSettings>())
            .Returns(Task.FromException<List<ImmichMemory>>(new HttpRequestException("saved memory unavailable", null, (HttpStatusCode)status)));
        var profile = new Profile { PhotoSource = memoryOnly ? "memories" : "all", Memories = new() { IncludeSaved = true } };
        var collector = new PlaylistAssetCollector(client);
        if (recovers)
        {
            var assets = await collector.CollectAsync(profile, new());
            CollectionAssert.AreEqual(new[] { "regular-photo" }, assets.Keys.ToArray());
        }
        else
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => collector.CollectAsync(profile, new()));
        }
        await client.DidNotReceive().GetMemoriesAsync(Arg.Any<ImmichSettings>(), Arg.Any<DateOnly>());
    }

    private static IImmichClient Mock(Exception failure)
    {
        var client = Substitute.For<IImmichClient>();
        client.SearchAssetsAllPagesAsync(Arg.Any<ImmichSettings>(), Arg.Any<SearchAssetsRequest>())
            .Returns([new ImmichAsset { Id = "regular-photo", Type = "IMAGE" }]);
        client.GetMemoriesAsync(Arg.Any<ImmichSettings>(), Arg.Any<DateOnly>())
            .Returns(Task.FromException<List<ImmichMemory>>(failure));
        return client;
    }
}
