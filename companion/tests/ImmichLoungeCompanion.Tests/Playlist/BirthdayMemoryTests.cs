using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.Playlist;

[TestClass]
public class BirthdayMemoryTests
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [TestMethod]
    [DataRow(true, false, "on-this-day")]
    [DataRow(false, true, "birthday")]
    [DataRow(true, true, "both")]
    public async Task MemorySources_AreIndependentAndDoNotFetchTheWholeLibrary(bool onThisDay, bool birthdays, string expected)
    {
        var client = Client("3.3.1");
        client.GetMemoriesAsync(Arg.Any<ImmichSettings>(), Arg.Any<DateOnly>()).Returns(
        [
            new() { Type = "on_this_day", Assets = [new() { Id = "on-this-day", Type = "IMAGE" }] },
            new() { Type = "birthday", Data = new() { PersonName = "Emma", Year = 2014 }, Assets = [new() { Id = "birthday", Type = "IMAGE" }] },
            new() { Type = "future-memory-type", Assets = [new() { Id = "unknown", Type = "IMAGE" }] }
        ]);
        var profile = new Profile { Memories = new() { IncludeBirthdays = birthdays } };
        if (onThisDay)
        {
            profile.ContentSources.Add(new() { Type = "memories" });
        }
        var result = await new PlaylistAssetCollector(client).CollectAsync(profile, new());

        CollectionAssert.AreEquivalent(expected == "both" ? new[] { "on-this-day", "birthday" } : new[] { expected }, result.Keys.ToArray());
        await client.DidNotReceive().SearchAssetsAllPagesAsync(Arg.Any<ImmichSettings>(), Arg.Any<SearchAssetsRequest>());
        if (birthdays)
        {
            Assert.AreEqual("Emma's birthday", PlaylistEntryProjector.CreateEntries(result, profile).Single(entry => entry.Id == "birthday").BirthdayCaption);
        }
    }

    [TestMethod]
    [DataRow("3.2.4")]
    [DataRow("3.3.0-prerelease")]
    [DataRow(null)]
    public async Task UnsupportedServer_DoesNotRequestBirthdayMemories(string? version)
    {
        var client = Client(version);
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            Memories = new() { IncludeBirthdays = true }
        }, new());
        Assert.HasCount(0, result);
        await client.DidNotReceive().GetMemoriesAsync(Arg.Any<ImmichSettings>(), Arg.Any<DateOnly>());
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task BirthdayContext_PreservesAlbumLabelAndDeduplicates(bool captions)
    {
        var client = Client("3.3.1");
        client.SearchAssetsAllPagesAsync(Arg.Any<ImmichSettings>(), Arg.Any<SearchAssetsRequest>())
            .Returns([new() { Id = "shared-photo", Type = "IMAGE" }]);
        client.GetMemoriesAsync(Arg.Any<ImmichSettings>(), Arg.Any<DateOnly>()).Returns(
        [
            new() { Type = "on_this_day", Assets = [new() { Id = "shared-photo", Type = "IMAGE" }] },
            new() { Type = "birthday", Data = new() { PersonName = "Emma" }, Assets = [new() { Id = "shared-photo", Type = "IMAGE" }] }
        ]);
        var profile = new Profile
        {
            ContentSources = [new() { Type = "album", Id = "family", Label = "Family" }, new() { Type = "memories" }],
            Memories = new() { IncludeBirthdays = true, ShowBirthdayCaptions = captions }
        };
        var result = await new PlaylistAssetCollector(client).CollectAsync(profile, new());
        var entry = PlaylistEntryProjector.CreateEntries(result, profile).Single();
        Assert.AreEqual("Family", entry.SourceLabel);
        Assert.AreEqual(captions ? "Emma's birthday" : null, entry.BirthdayCaption);
    }

    [TestMethod]
    public async Task BirthdayAssets_RespectLocationAndQuality()
    {
        var client = Client("3.3.1");
        client.GetMemoriesAsync(Arg.Any<ImmichSettings>(), Arg.Any<DateOnly>()).Returns(
        [
            new()
            {
                Type = "birthday", Data = new() { PersonName = "Emma" },
                Assets =
                [
                    Photo("kept", "Switzerland", 2000), Photo("wrong-place", "Canada", 2000),
                    Photo("too-small", "Switzerland", 10)
                ]
            }
        ]);
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            Memories = new() { IncludeBirthdays = true },
            LocationFilter = new() { Country = "Switzerland" },
            Quality = new() { MinFileSizeKb = 1000 }
        }, new());
        CollectionAssert.AreEqual(new[] { "kept" }, result.Keys.ToArray());
    }

    [TestMethod]
    [DataRow("2026-10-09T22:30:00Z", "Europe/Zurich", "2026-10-10")]
    [DataRow("2026-10-10T00:30:00Z", "America/Los_Angeles", "2026-10-09")]
    [DataRow("2026-03-29T22:30:00Z", "Europe/Zurich", "2026-03-30")]
    public async Task MemoryQuery_UsesSelectedTimeZoneIncludingDaylightSaving(string instant, string zone, string expectedDate)
    {
        var client = Client("3.3.1");
        client.GetMemoriesAsync(Arg.Any<ImmichSettings>(), Arg.Any<DateOnly>()).Returns([]);
        await new PlaylistAssetCollector(client, new FixedClock(DateTimeOffset.Parse(instant))).CollectAsync(new()
        {
            ContentSources = [new() { Type = "memories" }], Memories = new() { TimeZoneId = zone }
        }, new());
        await client.Received(1).GetMemoriesAsync(Arg.Any<ImmichSettings>(), DateOnly.Parse(expectedDate));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task AbbreviatedMemoryAssets_LoadMissingMetadataForFilters(bool stillExists)
    {
        var client = Client("3.3.1");
        client.GetMemoriesAsync(Arg.Any<ImmichSettings>(), Arg.Any<DateOnly>()).Returns(
        [
            new() { Type = "birthday", Data = new() { PersonName = "Emma" }, Assets = [new() { Id = "photo", Type = "IMAGE" }] }
        ]);
        client.GetAssetAsync(Arg.Any<ImmichSettings>(), "photo", Arg.Any<CancellationToken>())
            .Returns(stillExists ? Photo("photo", "Switzerland", 2000) : null);
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            Memories = new() { IncludeBirthdays = true },
            LocationFilter = new() { Country = "Switzerland" }, Quality = new() { MinFileSizeKb = 1000 }
        }, new());
        Assert.HasCount(stillExists ? 1 : 0, result);
        await client.Received(1).GetAssetAsync(Arg.Any<ImmichSettings>(), "photo", Arg.Any<CancellationToken>());
        if (stillExists)
        {
            Assert.AreEqual("Emma's birthday", result["photo"].BirthdayCaption);
        }
    }

    [TestMethod]
    public async Task MemoryMetadataAlreadyInSearch_DoesNotFetchDetailsAgain()
    {
        var client = Client("3.3.1");
        client.SearchAssetsAllPagesAsync(Arg.Any<ImmichSettings>(), Arg.Any<SearchAssetsRequest>())
            .Returns([Photo("photo", "Switzerland", 2000)]);
        client.GetMemoriesAsync(Arg.Any<ImmichSettings>(), Arg.Any<DateOnly>()).Returns(
        [
            new() { Type = "birthday", Data = new() { PersonName = "Emma" }, Assets = [new() { Id = "photo", Type = "IMAGE" }] }
        ]);
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            ContentSources = [new() { Type = "album", Id = "family", Label = "Family" }],
            Memories = new() { IncludeBirthdays = true },
            LocationFilter = new() { Country = "Switzerland" }, Quality = new() { MinFileSizeKb = 1000 }
        }, new());
        Assert.AreEqual("Emma's birthday", result["photo"].BirthdayCaption);
        Assert.AreEqual("Family", result["photo"].SourceLabel);
        await client.DidNotReceive().GetAssetAsync(Arg.Any<ImmichSettings>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static IImmichClient Client(string? version)
    {
        var client = Substitute.For<IImmichClient>();
        client.GetSearchApiCapabilitiesAsync(Arg.Any<ImmichSettings>(), Arg.Any<bool>()).Returns(new SearchApiCapabilities(version, true));
        return client;
    }

    private static ImmichAsset Photo(string id, string country, int sizeKb) => new()
    {
        Id = id, Type = "IMAGE", ExifInfo = new() { Country = country, FileSizeInByte = sizeKb * 1024L }
    };
}
