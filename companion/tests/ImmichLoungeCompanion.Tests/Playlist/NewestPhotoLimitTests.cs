using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.Playlist;

[TestClass]
public class NewestPhotoLimitTests
{
    private static readonly ImmichSettings Settings = new() { ServerUrl = "http://immich", ApiKey = "key" };
    private static ImmichAsset Photo(string id, string? date) => new()
    {
        Id = id, Type = "IMAGE", FileCreatedAt = date,
        ExifInfo = new() { DateTimeOriginal = date }
    };
    private static Dictionary<string, CollectedAsset> Collect(params ImmichAsset[] assets) =>
        assets.ToDictionary(asset => asset.Id, asset => new CollectedAsset(asset, "Family"));

    [TestMethod]
    [DataRow(true, "oldestFirst")]
    [DataRow(true, "newestFirst")]
    [DataRow(false, "oldestFirst")]
    [DataRow(false, "newestFirst")]
    public void MembershipIsIndependentOfShufflePlaybackOrderAndMemoryPriority(bool shuffle, string order)
    {
        var assets = Collect(Photo("old-memory", "2020-01-01"), Photo("middle", "2024-01-01"), Photo("newest", "2025-01-01"));
        assets["old-memory"] = assets["old-memory"] with { IsMemory = true, BirthdayCaption = "Birthday" };
        var profile = new Profile { NewestPhotoCount = 2, Memories = new() { Prioritize = true },
            Slideshow = new() { Shuffle = shuffle, DateOrder = order } };
        var entries = PlaylistEntryProjector.CreateEntries(assets, profile);
        CollectionAssert.AreEquivalent(new[] { "middle", "newest" }, entries.Select(entry => entry.Id).ToArray());
        Assert.AreEqual(3, assets.Count, "Projection must not mutate the collector's dictionary.");
        Assert.IsTrue(entries.All(entry => entry.SourceLabel == "Family"));
        if (!shuffle)
        {
            CollectionAssert.AreEqual(order == "oldestFirst" ? new[] { "middle", "newest" } : new[] { "newest", "middle" }, entries.Select(entry => entry.Id).ToArray());
        }
    }

    [TestMethod]
    public void CaptureDatesCompareInstantsAndUseFileDateFallbackWithStableTies()
    {
        var fallback = Photo("fallback", null);
        fallback.FileCreatedAt = "2025-01-01T10:30:00Z";
        var assets = Collect(Photo("offset", "2025-01-01T12:00:00+02:00"), Photo("tie-b", "2025-01-01T11:00:00Z"),
            Photo("tie-a", "2025-01-01T11:00:00Z"), fallback, Photo("undated", "invalid"));
        var entries = PlaylistEntryProjector.CreateEntries(assets, new() { NewestPhotoCount = 3, Slideshow = new() { Shuffle = false } });
        CollectionAssert.AreEqual(new[] { "tie-a", "tie-b", "fallback" }, entries.Select(entry => entry.Id).ToArray());
    }

    [TestMethod]
    public void HiddenLockedAndDisabledMediaDoNotConsumeTheLimit()
    {
        var hidden = Photo("hidden", "2026-01-01"); hidden.Visibility = "hidden";
        var locked = Photo("locked", "2026-01-01"); locked.Visibility = "locked";
        var video = Photo("video", "2026-01-01"); video.Type = "VIDEO";
        var live = Photo("live", "2025-01-01"); live.LivePhotoVideoId = "motion";
        var entries = PlaylistEntryProjector.CreateEntries(Collect(hidden, locked, video, live, Photo("older", "2024-01-01")),
            new() { NewestPhotoCount = 2, Slideshow = new() { Shuffle = false } });
        CollectionAssert.AreEqual(new[] { "live", "older" }, entries.Select(entry => entry.Id).ToArray());
        Assert.IsTrue(entries.All(entry => entry.Type == "photo"));
    }

    [TestMethod]
    [DataRow(null, 3)]
    [DataRow(1, 1)]
    [DataRow(100, 3)]
    public void NullIsUnlimitedAndLargeLimitsKeepAllMatches(int? limit, int expected)
    {
        var entries = PlaylistEntryProjector.CreateEntries(Collect(Photo("a", null), Photo("b", null), Photo("c", null)), new() { NewestPhotoCount = limit });
        Assert.AreEqual(expected, entries.Count);
        Assert.AreEqual(0, PlaylistEntryProjector.CreateEntries(new(), new() { NewestPhotoCount = limit }).Count);
    }

    [TestMethod]
    public async Task FavoritesRatingsAndQualityApplyBeforeLimit()
    {
        var client = Substitute.For<IImmichClient>();
        var photos = new[] { Photo("new-nonfavorite", "2026-01-01"), Photo("low-rating", "2025-01-01"),
            Photo("too-small", "2024-01-01"), Photo("good", "2023-01-01"), Photo("older", "2022-01-01") };
        foreach (var photo in photos) { photo.IsFavorite = true; photo.ExifInfo!.Rating = 5; photo.ExifInfo.FileSizeInByte = 4096; }
        photos[0].IsFavorite = false; photos[1].ExifInfo!.Rating = 2; photos[2].ExifInfo!.FileSizeInByte = 512;
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(photos.ToList());
        var profile = new Profile { NewestPhotoCount = 1, OnlyFavorites = true, MinimumRating = 4, Quality = new() { MinFileSizeKb = 1 } };
        var entries = PlaylistEntryProjector.CreateEntries(await new PlaylistAssetCollector(client).CollectAsync(profile, Settings), profile);
        CollectionAssert.AreEqual(new[] { "good" }, entries.Select(entry => entry.Id).ToArray());
    }

    [TestMethod]
    public async Task AbbreviatedMemoriesHydrateCaptureDatesAndPreserveContext()
    {
        var client = Substitute.For<IImmichClient>();
        client.GetMemoriesAsync(Settings, Arg.Any<DateOnly>()).Returns([new() { Assets = [Photo("old", "2020-01-01"), Photo("new", null)] }]);
        client.GetAssetAsync(Settings, "new", Arg.Any<CancellationToken>()).Returns(Photo("new", "2025-01-01"));
        var profile = new Profile { PhotoSource = "memories", NewestPhotoCount = 1, ContentSources = [new() { Type = "memories" }] };
        var entries = PlaylistEntryProjector.CreateEntries(await new PlaylistAssetCollector(client).CollectAsync(profile, Settings), profile);
        CollectionAssert.AreEqual(new[] { "new" }, entries.Select(entry => entry.Id).ToArray());
        Assert.IsTrue(entries[0].IsMemory);
        await client.Received(1).GetAssetAsync(Settings, "new", Arg.Any<CancellationToken>());
    }
}
