using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Storage;
using ImmichLoungeCompanion.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImmichLoungeCompanion.Tests.Storage;

[TestClass]
public class JsonSettingsRepositoryTests
{
    [TestMethod]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsDefaultSettings()
    {
        using var dir = new TempDataDirectory();
        var repo = new JsonSettingsRepository(dir.Path);

        var settings = await repo.LoadAsync();

        Assert.AreEqual("Home", settings.FriendlyName);
        Assert.AreEqual(1, settings.SchemaVersion);
        Assert.IsFalse(string.IsNullOrEmpty(settings.CompanionUuid));
    }

    [TestMethod]
    public async Task LoadAsync_WhenFileDoesNotExist_PersistsGeneratedUuid()
    {
        using var dir = new TempDataDirectory();
        var repo = new JsonSettingsRepository(dir.Path);

        var first = await repo.LoadAsync();
        var second = await repo.LoadAsync();

        Assert.AreEqual(first.CompanionUuid, second.CompanionUuid);
    }

    [TestMethod]
    public async Task LoadAsync_WhenFileDoesNotExist_DoesNotCreateSettingsFile()
    {
        using var dir = new TempDataDirectory();
        var repo = new JsonSettingsRepository(dir.Path);

        _ = await repo.LoadAsync();

        Assert.IsFalse(File.Exists(Path.Combine(dir.Path, "settings.json")));
    }

    [TestMethod]
    public async Task SaveAndLoad_RoundTrips()
    {
        using var dir = new TempDataDirectory();
        var repo = new JsonSettingsRepository(dir.Path);

        var settings = new GlobalSettings
        {
            FriendlyName = "Office",
            Immich = new() { ServerUrl = "http://192.168.1.10:2283", ApiKey = "key123" },
            CompanionUuid = "test-uuid"
        };
        await repo.SaveAsync(settings);

        var loaded = await repo.LoadAsync();

        Assert.AreEqual("Office", loaded.FriendlyName);
        Assert.AreEqual("http://192.168.1.10:2283", loaded.Immich.ServerUrl);
        Assert.AreEqual("key123", loaded.Immich.ApiKey);
        Assert.AreEqual("test-uuid", loaded.CompanionUuid);
    }
}
