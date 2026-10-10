using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Storage;
using ImmichLoungeCompanion.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.Storage;

[TestClass]
public class JsonProfileRepositoryTests
{
    [TestMethod]
    public async Task OldProfileWithoutLocationLoadsAndRetainsSources()
    {
        using var dir = new TempDataDirectory();
        Directory.CreateDirectory(Path.Combine(dir.Path, "profiles"));
        await File.WriteAllTextAsync(Path.Combine(dir.Path, "profiles", "legacy.json"),
            """{"schemaVersion":1,"id":"legacy","name":"Legacy","contentSources":[{"type":"album","id":"album","label":"Family"}]}""");
        var repo = new JsonProfileRepository(dir.Path);
        var profile = await repo.GetAsync("legacy");
        Assert.IsNotNull(profile);
        Assert.IsNull(profile.LocationFilter);
        Assert.AreEqual("album", profile.ContentSources.Single().Id);
        await repo.SaveAsync(profile);
        var reloaded = await repo.GetAsync("legacy");
        Assert.IsNull(reloaded!.LocationFilter);
        Assert.AreEqual("album", reloaded.ContentSources.Single().Id);
        Assert.AreEqual(1, reloaded.SchemaVersion);
    }

    private static Profile MakeProfile(string id) => new()
    {
        Id = id,
        Name = "Test " + id,
        ContentSources = [new() { Type = "album", Id = "a1", Label = "Fam" }]
    };

    [TestMethod]
    public async Task GetAllAsync_WhenNoneExist_ReturnsEmpty()
    {
        using var dir = new TempDataDirectory();
        var repo = new JsonProfileRepository(dir.Path);
        Assert.AreEqual(0, (await repo.GetAllAsync()).Count);
    }

    [TestMethod]
    public async Task SaveAndGet_RoundTrips()
    {
        using var dir = new TempDataDirectory();
        var repo = new JsonProfileRepository(dir.Path);
        var profile = MakeProfile("living-room");

        await repo.SaveAsync(profile);
        var loaded = await repo.GetAsync("living-room");

        Assert.IsNotNull(loaded);
        Assert.AreEqual("Test living-room", loaded!.Name);
        Assert.AreEqual(1, loaded.ContentSources.Count);
    }

    [TestMethod]
    public async Task GetAsync_WhenNotExists_ReturnsNull()
    {
        using var dir = new TempDataDirectory();
        var repo = new JsonProfileRepository(dir.Path);
        Assert.IsNull(await repo.GetAsync("nope"));
    }

    [TestMethod]
    public async Task DeleteAsync_WhenExists_RemovesProfile()
    {
        using var dir = new TempDataDirectory();
        var repo = new JsonProfileRepository(dir.Path);
        await repo.SaveAsync(MakeProfile("del-me"));

        var deleted = await repo.DeleteAsync("del-me");

        Assert.IsTrue(deleted);
        Assert.IsNull(await repo.GetAsync("del-me"));
    }

    [TestMethod]
    public async Task DeleteAsync_WhenNotExists_ReturnsFalse()
    {
        using var dir = new TempDataDirectory();
        var repo = new JsonProfileRepository(dir.Path);
        Assert.IsFalse(await repo.DeleteAsync("nope"));
    }

    [TestMethod]
    public async Task GetAllAsync_ReturnsSavedProfiles()
    {
        using var dir = new TempDataDirectory();
        var repo = new JsonProfileRepository(dir.Path);
        await repo.SaveAsync(MakeProfile("a"));
        await repo.SaveAsync(MakeProfile("b"));

        var all = await repo.GetAllAsync();
        Assert.AreEqual(2, all.Count);
    }

    [TestMethod]
    public async Task GetAllAsync_SkipsMalformedProfileAndKeepsHealthyProfiles()
    {
        using var dir = new TempDataDirectory();
        var logger = Substitute.For<ILogger<JsonProfileRepository>>();
        var repo = new JsonProfileRepository(dir.Path, logger);
        await repo.SaveAsync(MakeProfile("healthy"));
        var brokenPath = System.IO.Path.Combine(dir.Path, "profiles", "broken.json");
        await File.WriteAllTextAsync(brokenPath, "{");

        var all = await repo.GetAllAsync();

        Assert.AreEqual("healthy", all.Single().Id);
        Assert.AreEqual("{", await File.ReadAllTextAsync(brokenPath), "Unreadable profiles must be preserved for recovery.");
        Assert.IsNull(await repo.GetAsync("broken"));
        Assert.IsTrue(logger.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "Log" &&
            call.GetArguments()[0] is LogLevel.Warning &&
            call.GetArguments()[2]!.ToString()!.Contains(brokenPath, StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("{}")]
    [DataRow("{\"id\":null}")]
    [DataRow("{\"id\":\"broken\",\"slideshow\":null}")]
    public async Task GetAllAsync_SkipsMissingRequiredProfileData(string json)
    {
        using var dir = new TempDataDirectory();
        var repo = new JsonProfileRepository(dir.Path);
        await repo.SaveAsync(MakeProfile("healthy"));
        await File.WriteAllTextAsync(System.IO.Path.Combine(dir.Path, "profiles", "broken.json"), json);

        Assert.AreEqual("healthy", (await repo.GetAllAsync()).Single().Id);
        Assert.IsNull(await repo.GetAsync("broken"));
    }

    [TestMethod]
    public async Task GetAllAsync_SkipsLockedProfileAndRecoversWhenUnlocked()
    {
        using var dir = new TempDataDirectory();
        var repo = new JsonProfileRepository(dir.Path);
        await repo.SaveAsync(MakeProfile("healthy"));
        await repo.SaveAsync(MakeProfile("locked"));
        var lockedPath = System.IO.Path.Combine(dir.Path, "profiles", "locked.json");
        await using (var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var all = await repo.GetAllAsync();
            Assert.AreEqual("healthy", all.Single().Id);
            Assert.IsNull(await repo.GetAsync("locked"));
        }
        Assert.AreEqual(2, (await repo.GetAllAsync()).Count);
        Assert.IsNotNull(await repo.GetAsync("locked"));
    }

    [TestMethod]
    public async Task SaveAsync_AtomicallyReplacesProfileWhileExistingReaderKeepsItsSnapshot()
    {
        using var dir = new TempDataDirectory();
        var repo = new JsonProfileRepository(dir.Path);
        await repo.SaveAsync(MakeProfile("atomic"));
        var path = System.IO.Path.Combine(dir.Path, "profiles", "atomic.json");
        await using var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        var updated = MakeProfile("atomic");
        updated.Name = "Updated";

        await repo.SaveAsync(updated);

        var previous = await JsonSerializer.DeserializeAsync<Profile>(reader,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.AreEqual("Test atomic", previous!.Name);
        Assert.AreEqual("Updated", (await repo.GetAsync("atomic"))!.Name);
        Assert.AreEqual(1, Directory.GetFiles(System.IO.Path.GetDirectoryName(path)!).Length);
    }

    [TestMethod]
    public async Task SaveAsync_FailedReplacementPreservesOriginalAndCleansTemporaryFile()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("This failure scenario requires Windows delete-sharing semantics.");
        }
        using var dir = new TempDataDirectory();
        var repo = new JsonProfileRepository(dir.Path);
        await repo.SaveAsync(MakeProfile("locked-save"));
        var path = System.IO.Path.Combine(dir.Path, "profiles", "locked-save.json");
        await using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var updated = MakeProfile("locked-save");
            updated.Name = "Updated";
            try
            {
                await repo.SaveAsync(updated);
                Assert.Fail("Replacing a file without delete sharing should fail.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The original file must remain intact even if the filesystem rejects replacement.
            }
        }
        Assert.AreEqual("Test locked-save", (await repo.GetAsync("locked-save"))!.Name);
        Assert.AreEqual(1, Directory.GetFiles(System.IO.Path.GetDirectoryName(path)!).Length);
    }
}
