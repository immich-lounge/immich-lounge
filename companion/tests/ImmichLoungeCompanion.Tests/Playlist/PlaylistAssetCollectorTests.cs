using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.Playlist;

[TestClass]
public class PlaylistAssetCollectorTests
{
    [TestMethod]
    public async Task CollectAsync_LocationAppliesToEveryLeafAndMemories()
    {
        var client = Substitute.For<IImmichClient>();
        var requests = new List<SearchAssetsRequest>();
        client.SearchAssetsAllPagesAsync(Arg.Any<ImmichSettings>(), Arg.Any<SearchAssetsRequest>())
            .Returns(call =>
            {
                requests.Add(call.Arg<SearchAssetsRequest>());
                return new List<ImmichAsset> { new() { Id = "search" } };
            });
        client.GetMemoriesAsync(Arg.Any<ImmichSettings>(), Arg.Any<DateOnly>()).Returns(
        [
            new ImmichMemory
            {
                Assets =
                [
                    new() { Id = "matching", ExifInfo = new() { Country = "Canada", State = "British Columbia", City = "Vancouver" } },
                    new() { Id = "wrong-country", ExifInfo = new() { Country = "USA", State = "British Columbia", City = "Vancouver" } },
                    new() { Id = "wrong-state", ExifInfo = new() { Country = "Canada", State = "Ontario", City = "Vancouver" } },
                    new() { Id = "wrong-city", ExifInfo = new() { Country = "Canada", State = "British Columbia", City = "Victoria" } },
                    new() { Id = "missing" }
                ]
            }
        ]);
        var profile = new Profile
        {
            ContentSources =
            [
                new() { Type = "album", Id = "album" },
                new() { Type = "person", Id = "person" },
                new() { Type = "tag", Id = "tag" },
                new() { Type = "memories" }
            ],
            DateFilter = new() { Type = "range", From = "2020-01-01" },
            LocationFilter = new() { Country = " Canada ", State = "British Columbia", City = "Vancouver" }
        };
        var assets = await new PlaylistAssetCollector(client).CollectAsync(profile, FakeImmich);
        Assert.AreEqual(3, requests.Count);
        foreach (var request in requests)
        {
            Assert.AreEqual("Canada", request.Country);
            Assert.AreEqual("British Columbia", request.State);
            Assert.AreEqual("Vancouver", request.City);
            Assert.AreEqual("2020-01-01", request.TakenAfter);
        }
        CollectionAssert.AreEquivalent(new[] { "search", "matching" }, assets.Keys.ToArray());
    }

    [TestMethod]
    public async Task CollectAsync_LocationOnlyFiltersAllAssetsAndBlankFieldsAreUnrestricted()
    {
        var client = Substitute.For<IImmichClient>();
        SearchAssetsRequest? captured = null;
        client.SearchAssetsAllPagesAsync(Arg.Any<ImmichSettings>(), Arg.Any<SearchAssetsRequest>())
            .Returns(call =>
            {
                captured = call.Arg<SearchAssetsRequest>();
                return new List<ImmichAsset>();
            });
        await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            LocationFilter = new() { City = " Zürich ", Country = "  " }
        }, FakeImmich);
        Assert.IsNotNull(captured);
        Assert.AreEqual("Zürich", captured.City);
        Assert.IsNull(captured.Country);
        Assert.IsNull(captured.State);
        Assert.IsNull(captured.AlbumIds);
    }

    private static ImmichSettings FakeImmich => new() { ServerUrl = "http://immich", ApiKey = "k" };

    [TestMethod]
    public async Task CollectAsync_AddsMemoriesOutsideRuleTree()
    {
        var client = Substitute.For<IImmichClient>();
        client.SearchAssetsAllPagesAsync(FakeImmich, Arg.Any<SearchAssetsRequest>())
            .Returns([new ImmichAsset { Id = "album-1", Type = "IMAGE" }]);
        client.GetMemoriesAsync(FakeImmich, Arg.Any<DateOnly>())
            .Returns([
                new ImmichMemory
                {
                    Assets =
                    [
                        new ImmichAsset { Id = "memory-1", Type = "IMAGE" }
                    ]
                }
            ]);

        var collector = new PlaylistAssetCollector(client);
        var profile = new Profile
        {
            ContentSources =
            [
                new() { Type = "album", Id = "a1", Label = "Album" },
                new() { Type = "memories", Id = "", Label = "Memories" }
            ]
        };

        var assets = await collector.CollectAsync(profile, FakeImmich);

        Assert.AreEqual(2, assets.Count);
        Assert.IsTrue(assets.ContainsKey("album-1"));
        Assert.IsTrue(assets.ContainsKey("memory-1"));
        Assert.IsNull(assets["memory-1"].SourceLabel);
    }

    [TestMethod]
    public async Task CollectAsync_NoRulesFetchesAllAssets()
    {
        var client = Substitute.For<IImmichClient>();
        SearchAssetsRequest? capturedRequest = null;
        client.SearchAssetsAllPagesAsync(FakeImmich, Arg.Any<SearchAssetsRequest>())
            .Returns(callInfo =>
            {
                capturedRequest = callInfo.Arg<SearchAssetsRequest>();
                return [new ImmichAsset { Id = "all-1", Type = "IMAGE" }];
            });

        var collector = new PlaylistAssetCollector(client);
        var profile = new Profile();

        var assets = await collector.CollectAsync(profile, FakeImmich);

        Assert.AreEqual(1, assets.Count);
        Assert.IsNotNull(capturedRequest);
        Assert.IsNull(capturedRequest.AlbumIds);
        Assert.IsNull(capturedRequest.PersonIds);
        Assert.IsNull(capturedRequest.TagIds);
    }

    [TestMethod]
    public async Task CollectAsync_DedupesDuplicateAssetsReturnedByRuleLeaf()
    {
        var client = Substitute.For<IImmichClient>();
        client.SearchAssetsAllPagesAsync(FakeImmich, Arg.Any<SearchAssetsRequest>())
            .Returns(
            [
                new ImmichAsset { Id = "dup-1", Type = "IMAGE" },
                new ImmichAsset { Id = "dup-1", Type = "IMAGE" },
                new ImmichAsset { Id = "unique-1", Type = "IMAGE" }
            ]);

        var collector = new PlaylistAssetCollector(client);
        var profile = new Profile
        {
            AssetFilter = new AssetFilterRule
            {
                Kind = AssetFilterRuleKind.Condition,
                Type = AssetFilterConditionType.Album,
                Id = "album-1",
                Label = "Album 1"
            }
        };

        var assets = await collector.CollectAsync(profile, FakeImmich);

        Assert.AreEqual(2, assets.Count);
        Assert.IsTrue(assets.ContainsKey("dup-1"));
        Assert.IsTrue(assets.ContainsKey("unique-1"));
        Assert.AreEqual("Album 1", assets["dup-1"].SourceLabel);
    }

    [TestMethod]
    public async Task CollectAsync_FiltersAssetsBelowMinimumFileSize()
    {
        var client = Substitute.For<IImmichClient>();
        client.SearchAssetsAllPagesAsync(FakeImmich, Arg.Any<SearchAssetsRequest>())
            .Returns(
            [
                new ImmichAsset
                {
                    Id = "small-1",
                    Type = "IMAGE",
                    ExifInfo = new ImmichExifInfo { FileSizeInByte = 350 * 1024L, ExifImageWidth = 1280, ExifImageHeight = 720 }
                },
                new ImmichAsset
                {
                    Id = "large-1",
                    Type = "IMAGE",
                    ExifInfo = new ImmichExifInfo { FileSizeInByte = 1800 * 1024L, ExifImageWidth = 3840, ExifImageHeight = 2160 }
                }
            ]);

        var collector = new PlaylistAssetCollector(client);
        var profile = new Profile
        {
            Quality = new QualitySettings { MinFileSizeKb = 1000 }
        };

        var assets = await collector.CollectAsync(profile, FakeImmich);

        Assert.AreEqual(1, assets.Count);
        Assert.IsFalse(assets.ContainsKey("small-1"));
        Assert.IsTrue(assets.ContainsKey("large-1"));
    }
}
