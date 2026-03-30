using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Storage;
using ImmichLoungeCompanion.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImmichLoungeCompanion.Tests.Storage;

[TestClass]
public class JsonProfileRepositoryTests
{
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
}
