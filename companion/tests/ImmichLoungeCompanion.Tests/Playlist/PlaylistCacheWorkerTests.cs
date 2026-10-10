using System.Reflection;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using ImmichLoungeCompanion.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.Playlist;

[TestClass]
public sealed class PlaylistCacheWorkerTests
{
    [TestMethod]
    public async Task ProfileEnumerationFailure_IsLoggedAndNextRefreshStillBuilds()
    {
        var profiles = Substitute.For<IProfileRepository>();
        var cache = Substitute.For<IPlaylistCache>();
        var builder = Substitute.For<IPlaylistBuilder>();
        var logger = Substitute.For<ILogger<PlaylistCacheWorker>>();
        var profile = new Profile { Id = "healthy", Name = "Healthy" };
        profiles.GetAllAsync().Returns(
            _ => throw new IOException("Temporary profile directory read failure"),
            _ => [profile]);
        profiles.GetAsync("healthy").Returns(profile);
        cache.Get("healthy").Returns(new PlaylistCacheEntry([], DateTimeOffset.UtcNow.AddHours(-2)));
        cache.TryStartBuilding("healthy").Returns(true);
        builder.BuildAsync(profile, Arg.Any<CancellationToken>()).Returns([new PlaylistEntry("photo", "photo", null, null)]);
        var rebuilt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        cache.When(c => c.Set("healthy", Arg.Any<PlaylistCacheEntry>())).Do(_ => rebuilt.TrySetResult());
        using var worker = new PlaylistCacheWorker(builder, cache, profiles, logger);

        await RefreshAsync(worker);
        Assert.IsTrue(logger.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "Log" &&
            call.GetArguments()[0] is LogLevel.Error && call.GetArguments()[3] is IOException));
        await RefreshAsync(worker);
        await rebuilt.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static Task RefreshAsync(PlaylistCacheWorker worker) =>
        (Task)typeof(PlaylistCacheWorker).GetMethod("RebuildExpiredCachesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(worker, [CancellationToken.None])!;
}
