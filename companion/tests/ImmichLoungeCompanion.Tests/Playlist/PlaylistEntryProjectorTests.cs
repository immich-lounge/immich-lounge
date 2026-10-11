using System.Collections.Generic;
using System.Linq;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImmichLoungeCompanion.Tests.Playlist;

[TestClass]
public class PlaylistEntryProjectorTests
{
    [TestMethod]
    [DataRow("newestFirst", "memory-new", "memory-old", "regular-new", "regular-old")]
    [DataRow("oldestFirst", "memory-old", "memory-new", "regular-old", "regular-new")]
    public void MemoryPriority_PreservesChosenDateOrderWithinBothGroups(string order, string first, string second, string third, string fourth)
    {
        var profile = new Profile
        {
            Slideshow = new() { Shuffle = false, DateOrder = order },
            Memories = new() { Prioritize = true }
        };
        var assets = new Dictionary<string, CollectedAsset>();
        foreach (var (id, date, memory) in new[] {
            ("regular-new", "2025-01-01T00:00:00Z", false),
            ("memory-old", "2023-01-01T00:00:00Z", true),
            ("regular-old", "2022-01-01T00:00:00Z", false),
            ("memory-new", "2024-01-01T00:00:00Z", true) })
        {
            assets[id] = new(new ImmichAsset { Id = id, Type = "IMAGE",
                ExifInfo = new() { DateTimeOriginal = date } }, null, IsMemory: memory);
        }
        var entries = PlaylistEntryProjector.CreateEntries(assets, profile);
        CollectionAssert.AreEqual(new[] { first, second, third, fourth }, entries.Select(entry => entry.Id).ToArray());
    }

    [TestMethod]
    [DataRow("newestFirst", "newer", "same-a", "same-b", "older", "undated")]
    [DataRow("oldestFirst", "undated", "older", "same-a", "same-b", "newer")]
    public void DateOrder_SortsDatesAndUsesStableIdTieBreaks(string order, string first, string second, string third, string fourth, string fifth)
    {
        var profile = new Profile { Slideshow = new() { Shuffle = false, DateOrder = order } };
        var assets = new Dictionary<string, CollectedAsset>();
        foreach (var (id, date) in new[] {
            ("same-b", "2024-06-01T00:00:00Z"),
            ("newer", "2025-01-01T00:00:00Z"),
            ("undated", (string?)null),
            ("older", "2024-01-01T00:00:00Z"),
            ("same-a", "2024-06-01T00:00:00Z") })
        {
            assets[id] = new(new ImmichAsset { Id = id, Type = "IMAGE",
                ExifInfo = date == null ? null : new ImmichExifInfo { DateTimeOriginal = date } }, null);
        }
        var entries = PlaylistEntryProjector.CreateEntries(assets, profile);
        CollectionAssert.AreEqual(new[] { first, second, third, fourth, fifth }, entries.Select(entry => entry.Id).ToArray());
    }

    [TestMethod]
    public void CreateEntries_WhenShuffleDisabled_SortsNewestFirst()
    {
        var profile = new Profile
        {
            MediaTypes = new() { Photos = true },
            Slideshow = new() { Shuffle = false }
        };

        var assets = new Dictionary<string, CollectedAsset>
        {
            ["older"] = new(new ImmichAsset
            {
                Id = "older",
                Type = "IMAGE",
                ExifInfo = new ImmichExifInfo { DateTimeOriginal = "2024-01-01T00:00:00Z" }
            }, null),
            ["newer"] = new(new ImmichAsset
            {
                Id = "newer",
                Type = "IMAGE",
                ExifInfo = new ImmichExifInfo { DateTimeOriginal = "2025-01-01T00:00:00Z" }
            }, null)
        };

        var entries = PlaylistEntryProjector.CreateEntries(assets, profile);

        CollectionAssert.AreEqual(new[] { "newer", "older" }, entries.Select(entry => entry.Id).ToArray());
    }

    [TestMethod]
    public void CreateEntries_FiltersOutDisabledMediaTypes()
    {
        var profile = new Profile
        {
            MediaTypes = new() { Photos = true, Videos = false, LivePhotos = false }
        };

        var assets = new Dictionary<string, CollectedAsset>
        {
            ["photo-1"] = new(new ImmichAsset { Id = "photo-1", Type = "IMAGE" }, null),
            ["video-1"] = new(new ImmichAsset { Id = "video-1", Type = "VIDEO" }, null)
        };

        var entries = PlaylistEntryProjector.CreateEntries(assets, profile);

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("photo-1", entries[0].Id);
        Assert.AreEqual("photo", entries[0].Type);
    }
}
