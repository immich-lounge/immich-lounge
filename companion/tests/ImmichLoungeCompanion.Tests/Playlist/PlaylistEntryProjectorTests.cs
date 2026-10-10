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
