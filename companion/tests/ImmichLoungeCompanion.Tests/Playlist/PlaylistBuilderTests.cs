using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using ImmichLoungeCompanion.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.Playlist;

[TestClass]
public class PlaylistBuilderTests
{
    private static ImmichSettings FakeImmich => new() { ServerUrl = "http://immich", ApiKey = "k" };
    private static GlobalSettings FakeSettings => new() { Immich = FakeImmich };
    private static PlaylistBuilder CreateBuilder(IImmichClient client, ISettingsRepository settingsRepo)
        => new(settingsRepo, new PlaylistAssetCollector(client));

    private static ImmichAsset Photo(string id, string? liveVideoId = null) => new()
    {
        Id = id,
        Type = "IMAGE",
        LivePhotoVideoId = liveVideoId
    };
    private static ImmichAsset PhotoWithDate(string id, string date) => new()
    {
        Id = id,
        Type = "IMAGE",
        ExifInfo = new ImmichExifInfo { DateTimeOriginal = date }
    };
    private static ImmichAsset Video(string id) => new() { Id = id, Type = "VIDEO" };

    [TestMethod]
    public async Task Build_AlbumSource_ReturnsPhotoEntries()
    {
        var client = Substitute.For<IImmichClient>();
        var settingsRepo = Substitute.For<ISettingsRepository>();
        settingsRepo.LoadAsync().Returns(FakeSettings);
        client.SearchAssetsAllPagesAsync(FakeImmich, Arg.Any<SearchAssetsRequest>())
              .Returns([Photo("p1"), Photo("p2")]);

        var builder = CreateBuilder(client, settingsRepo);
        var profile = new Profile
        {
            ContentSources = [new() { Type = "album", Id = "a1", Label = "Fam" }],
            MediaTypes = new() { Photos = true }
        };

        var entries = await builder.BuildAsync(profile);

        Assert.AreEqual(2, entries.Count);
        foreach (var item in entries)
        {
            Assert.IsTrue(item.Type == "photo");
        }

        Assert.IsTrue(entries.Any(e => e.SourceLabel == "Fam"));
    }

    [TestMethod]
    public async Task Build_DeduplicatesAcrossSources()
    {
        var client = Substitute.For<IImmichClient>();
        var settingsRepo = Substitute.For<ISettingsRepository>();
        settingsRepo.LoadAsync().Returns(FakeSettings);
        // Both sources return the same asset ids
        client.SearchAssetsAllPagesAsync(FakeImmich, Arg.Any<SearchAssetsRequest>())
              .Returns([Photo("shared"), Photo("unique")]);

        var builder = CreateBuilder(client, settingsRepo);
        var profile = new Profile
        {
            ContentSources =
            [
                new() { Type = "album", Id = "a1", Label = "A" },
                new() { Type = "album", Id = "a2", Label = "B" }
            ],
            MediaTypes = new() { Photos = true }
        };

        var entries = await builder.BuildAsync(profile);

        // Both sources return same 2 assets; after dedup only 2 unique entries remain
        Assert.AreEqual(2, entries.Count);
        Assert.AreEqual(2, entries.Select(e => e.Id).Distinct().Count());
    }

    [TestMethod]
    public async Task Build_FiltersOutVideoWhenOnlyPhotosEnabled()
    {
        var client = Substitute.For<IImmichClient>();
        var settingsRepo = Substitute.For<ISettingsRepository>();
        settingsRepo.LoadAsync().Returns(FakeSettings);
        client.SearchAssetsAllPagesAsync(FakeImmich, Arg.Any<SearchAssetsRequest>())
              .Returns([Photo("p1"), Video("v1")]);

        var builder = CreateBuilder(client, settingsRepo);
        var profile = new Profile
        {
            ContentSources = [new() { Type = "album", Id = "a1", Label = "A" }],
            MediaTypes = new() { Photos = true, Videos = false }
        };

        var entries = await builder.BuildAsync(profile);

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("photo", entries[0].Type);
    }

    [TestMethod]
    public async Task Build_LivePhoto_SetsCorrectTypeAndVideoId()
    {
        var client = Substitute.For<IImmichClient>();
        var settingsRepo = Substitute.For<ISettingsRepository>();
        settingsRepo.LoadAsync().Returns(FakeSettings);
        client.SearchAssetsAllPagesAsync(FakeImmich, Arg.Any<SearchAssetsRequest>())
              .Returns([Photo("photo1", liveVideoId: "video1")]);

        var builder = CreateBuilder(client, settingsRepo);
        var profile = new Profile
        {
            ContentSources = [new() { Type = "album", Id = "a1", Label = "A" }],
            MediaTypes = new() { Photos = true, LivePhotos = true }
        };

        var entries = await builder.BuildAsync(profile);

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("livePhoto", entries[0].Type);
        Assert.AreEqual("video1", entries[0].LivePhotoVideoId);
    }

    [TestMethod]
    public async Task Build_LivePhoto_FilteredOutWhenLivePhotosDisabled()
    {
        var client = Substitute.For<IImmichClient>();
        var settingsRepo = Substitute.For<ISettingsRepository>();
        settingsRepo.LoadAsync().Returns(FakeSettings);
        client.SearchAssetsAllPagesAsync(FakeImmich, Arg.Any<SearchAssetsRequest>())
              .Returns([Photo("photo1", liveVideoId: "video1"), Photo("photo2")]);

        var builder = CreateBuilder(client, settingsRepo);
        var profile = new Profile
        {
            ContentSources = [new() { Type = "album", Id = "a1", Label = "A" }],
            MediaTypes = new() { Photos = true, LivePhotos = false }
        };

        var entries = await builder.BuildAsync(profile);

        // live photo treated as regular photo (still shown, just not as video)
        Assert.AreEqual(2, entries.Count);
        foreach (var item in entries)
        {
            Assert.IsTrue(item.Type == "photo");
        }
    }

    [TestMethod]
    public async Task Build_WhenShuffleDisabled_SortsByDateDescending()
    {
        var client = Substitute.For<IImmichClient>();
        var settingsRepo = Substitute.For<ISettingsRepository>();
        settingsRepo.LoadAsync().Returns(FakeSettings);
        client.SearchAssetsAllPagesAsync(FakeImmich, Arg.Any<SearchAssetsRequest>())
              .Returns([PhotoWithDate("older", "2024-01-01T00:00:00Z"), PhotoWithDate("newer", "2025-01-01T00:00:00Z")]);

        var builder = CreateBuilder(client, settingsRepo);
        var profile = new Profile
        {
            ContentSources = [new() { Type = "album", Id = "a1", Label = "A" }],
            MediaTypes = new() { Photos = true },
            Slideshow = new() { Shuffle = false }
        };

        var entries = await builder.BuildAsync(profile);

        CollectionAssert.AreEqual(new[] { "newer", "older" }, entries.Select(e => e.Id).ToArray());
    }

    [TestMethod]
    public async Task Build_AssetFilterAnd_IntersectsChildResults()
    {
        var client = Substitute.For<IImmichClient>();
        var settingsRepo = Substitute.For<ISettingsRepository>();
        settingsRepo.LoadAsync().Returns(FakeSettings);
        client.SearchAssetsAllPagesAsync(FakeImmich, Arg.Any<SearchAssetsRequest>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<SearchAssetsRequest>();
                if (request.PersonIds?.Contains("p1") == true)
                {
                    return [Photo("shared"), Photo("person-only")];
                }

                if (request.AlbumIds?.Contains("a1") == true)
                {
                    return [Photo("shared"), Photo("album-only")];
                }

                return [];
            });

        var builder = CreateBuilder(client, settingsRepo);
        var profile = new Profile
        {
            AssetFilter = new()
            {
                Kind = AssetFilterRuleKind.Group,
                Operator = AssetFilterGroupOperator.And,
                Children =
                [
                    new()
                    {
                        Kind = AssetFilterRuleKind.Condition,
                        Type = AssetFilterConditionType.Person,
                        Id = "p1",
                        Label = "Alice"
                    },
                    new()
                    {
                        Kind = AssetFilterRuleKind.Condition,
                        Type = AssetFilterConditionType.Album,
                        Id = "a1",
                        Label = "Family"
                    }
                ]
            },
            MediaTypes = new() { Photos = true }
        };

        var entries = await builder.BuildAsync(profile);

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("shared", entries[0].Id);
        Assert.IsNull(entries[0].SourceLabel);
    }

    [TestMethod]
    public async Task Build_AssetFilterNestedGroup_CombinesAndOrRules()
    {
        var client = Substitute.For<IImmichClient>();
        var settingsRepo = Substitute.For<ISettingsRepository>();
        settingsRepo.LoadAsync().Returns(FakeSettings);
        client.SearchAssetsAllPagesAsync(FakeImmich, Arg.Any<SearchAssetsRequest>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<SearchAssetsRequest>();
                if (request.PersonIds?.Contains("p1") == true)
                {
                    return [Photo("shared"), Photo("p1-only")];
                }

                if (request.PersonIds?.Contains("p2") == true)
                {
                    return [Photo("shared"), Photo("p2-only")];
                }

                if (request.AlbumIds?.Contains("a1") == true)
                {
                    return [Photo("shared"), Photo("album-only")];
                }

                return [];
            });

        var builder = CreateBuilder(client, settingsRepo);
        var profile = new Profile
        {
            AssetFilter = new()
            {
                Kind = AssetFilterRuleKind.Group,
                Operator = AssetFilterGroupOperator.And,
                Children =
                [
                    new()
                    {
                        Kind = AssetFilterRuleKind.Group,
                        Operator = AssetFilterGroupOperator.Or,
                        Children =
                        [
                            new()
                            {
                                Kind = AssetFilterRuleKind.Condition,
                                Type = AssetFilterConditionType.Person,
                                Id = "p1",
                                Label = "Alice"
                            },
                            new()
                            {
                                Kind = AssetFilterRuleKind.Condition,
                                Type = AssetFilterConditionType.Person,
                                Id = "p2",
                                Label = "Bob"
                            }
                        ]
                    },
                    new()
                    {
                        Kind = AssetFilterRuleKind.Condition,
                        Type = AssetFilterConditionType.Album,
                        Id = "a1",
                        Label = "Family"
                    }
                ]
            },
            MediaTypes = new() { Photos = true }
        };

        var entries = await builder.BuildAsync(profile);

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("shared", entries[0].Id);
    }

    [TestMethod]
    public async Task Build_ContentSourcesAreNormalizedIntoOrFilter()
    {
        var client = Substitute.For<IImmichClient>();
        var settingsRepo = Substitute.For<ISettingsRepository>();
        settingsRepo.LoadAsync().Returns(FakeSettings);
        client.SearchAssetsAllPagesAsync(FakeImmich, Arg.Any<SearchAssetsRequest>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<SearchAssetsRequest>();
                if (request.AlbumIds?.Contains("a1") == true)
                {
                    return [Photo("album-1")];
                }

                if (request.PersonIds?.Contains("p1") == true)
                {
                    return [Photo("person-1")];
                }

                return [];
            });

        var builder = CreateBuilder(client, settingsRepo);
        var profile = new Profile
        {
            ContentSources =
            [
                new() { Type = "album", Id = "a1", Label = "Family" },
                new() { Type = "person", Id = "p1", Label = "Alice" }
            ],
            MediaTypes = new() { Photos = true }
        };

        var entries = await builder.BuildAsync(profile);

        Assert.AreEqual(2, entries.Count);
        Assert.AreEqual(2, entries.Select(e => e.Id).Distinct().Count());
        Assert.IsNotNull(profile.AssetFilter);
        Assert.AreEqual(AssetFilterRuleKind.Group, profile.AssetFilter.Kind);
        Assert.AreEqual(AssetFilterGroupOperator.Or, profile.AssetFilter.Operator);
    }
}
