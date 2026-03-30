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
