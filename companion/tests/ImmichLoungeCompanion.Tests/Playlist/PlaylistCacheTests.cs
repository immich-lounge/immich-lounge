using System.Reflection;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using ImmichLoungeCompanion.Tests.Helpers;

namespace ImmichLoungeCompanion.Tests.Playlist;

[TestClass]
public sealed class PlaylistCacheTests
{
    [TestMethod]
    public void ProfileInvalidation_RejectsOnlyAffectedBuildAndAcceptsNewGeneration()
    {
        using var dir = new TempDataDirectory();
        var cache = new PlaylistCache(dir.Path);
        var affected = cache.GetGeneration("edited");
        var unaffected = cache.GetGeneration("other");
        cache.Invalidate("edited");

        Assert.IsFalse(cache.TrySet("edited", Entry("old"), affected));
        Assert.IsTrue(cache.TrySet("other", Entry("unaffected"), unaffected));
        Assert.IsTrue(cache.TrySet("edited", Entry("new"), cache.GetGeneration("edited")));
        Assert.AreEqual("new", cache.Get("edited")!.Assets.Single().Id);
        Assert.AreEqual("unaffected", cache.Get("other")!.Assets.Single().Id);
        cache.InvalidateAll();
    }

    [TestMethod]
    public void GlobalInvalidation_RejectsAllPreviousGenerationsIncludingProfileSpecificOnes()
    {
        using var dir = new TempDataDirectory();
        var cache = new PlaylistCache(dir.Path);
        cache.Invalidate("edited");
        var affected = cache.GetGeneration("edited");
        var untouched = cache.GetGeneration("other");
        cache.InvalidateAll();

        Assert.IsFalse(cache.TrySet("edited", Entry("old"), affected));
        Assert.IsFalse(cache.TrySet("other", Entry("old"), untouched));
        Assert.IsTrue(cache.TrySet("edited", Entry("new"), cache.GetGeneration("edited")));
        cache.InvalidateAll();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DelayedPersistenceAfterInvalidation_DoesNotRestoreDiskCache(bool invalidateAll)
    {
        using var dir = new TempDataDirectory();
        var cache = new PlaylistCache(dir.Path);
        var stale = Entry("old");
        cache.Set("edited", stale);
        await PersistAsync(cache, "edited", stale);
        if (invalidateAll)
        {
            cache.InvalidateAll();
        }
        else
        {
            cache.Invalidate("edited");
        }

        // Run an old queued persistence operation after invalidation, without timing-dependent sleeps.
        await PersistAsync(cache, "edited", stale);

        Assert.IsNull(cache.Get("edited"));
        Assert.IsNull(new PlaylistCache(dir.Path).Get("edited"), "Restart must not resurrect the invalidated playlist.");
    }

    [TestMethod]
    public void DelayedPersistenceOfSupersededEntry_KeepsNewestDiskCache()
    {
        using var dir = new TempDataDirectory();
        var cache = new PlaylistCache(dir.Path);
        var old = Entry("old");
        var current = Entry("current");
        // Keep queued writes out of the whole probe: a later current write must
        // not repair an incorrect stale write before the simulated restart reads it.
        var persistenceGate = typeof(PlaylistCache).GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(cache)!;
        lock (persistenceGate)
        {
            cache.Set("edited", old);
            cache.Set("edited", current);
            PersistSynchronously(cache, "edited", current);
            PersistSynchronously(cache, "edited", old);
            Assert.AreEqual("current", new PlaylistCache(dir.Path).Get("edited")!.Assets.Single().Id);
            cache.InvalidateAll();
        }
    }

    private static PlaylistCacheEntry Entry(string id) =>
        new([new PlaylistEntry(id, id, null, null)], DateTimeOffset.UtcNow);

    private static Task PersistAsync(PlaylistCache cache, string id, PlaylistCacheEntry entry) =>
        (Task)typeof(PlaylistCache).GetMethod("PersistToDiskAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(cache, [id, entry])!;

    private static void PersistSynchronously(PlaylistCache cache, string id, PlaylistCacheEntry entry)
    {
        var persistence = PersistAsync(cache, id, entry);
        Assert.IsTrue(persistence.IsCompleted, "The locked persistence probe must complete synchronously.");
        persistence.GetAwaiter().GetResult();
    }
}
