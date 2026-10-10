using System.Reflection;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using ImmichLoungeCompanion.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ImmichLoungeCompanion.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImmichLoungeCompanion.Tests.Playlist;

[TestClass]
public sealed class PlaylistCacheWorkerTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task InvalidationDuringBuild_DiscardsOldResultAndAllowsFreshBuild(bool invalidateAll, bool coldStart)
    {
        using var dir = new TempDataDirectory();
        var cache = new PlaylistCache(dir.Path);
        var profiles = Substitute.For<IProfileRepository>();
        var builder = Substitute.For<IPlaylistBuilder>();
        var oldProfile = new Profile { Id = "edited", Name = "Old selection" };
        var newProfile = new Profile { Id = "edited", Name = "New selection" };
        profiles.GetAsync("edited").Returns(oldProfile);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<List<PlaylistEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
        builder.BuildAsync(oldProfile, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            started.TrySetResult();
            return finish.Task;
        });
        using var worker = new PlaylistCacheWorker(builder, cache, profiles, NullLogger<PlaylistCacheWorker>.Instance);
        if (coldStart)
        {
            Assert.IsTrue(cache.TryStartBuilding("edited"));
        }
        var build = worker.RebuildAsync("edited", coldStart);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        profiles.GetAsync("edited").Returns(newProfile);
        if (invalidateAll)
        {
            cache.InvalidateAll();
        }
        else
        {
            cache.Invalidate("edited");
        }
        finish.SetResult([new PlaylistEntry("old-photo", "old-photo", null, null)]);
        await build;

        Assert.IsNull(cache.Get("edited"), "A build started before invalidation must not republish old photos.");
        Assert.IsFalse(cache.IsBuilding("edited"));
        builder.BuildAsync(newProfile, Arg.Any<CancellationToken>()).Returns([new PlaylistEntry("new-photo", "new-photo", null, null)]);
        await worker.RebuildAsync("edited");
        Assert.AreEqual("new-photo", cache.Get("edited")!.Assets.Single().Id);
        cache.InvalidateAll(); // Let queued writes finish or become stale before disposing the data directory.
    }

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
        cache.TrySet("healthy", Arg.Any<PlaylistCacheEntry>(), Arg.Any<long>()).Returns(_ =>
        {
            rebuilt.TrySetResult();
            return true;
        });
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
