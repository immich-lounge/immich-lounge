using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using NSubstitute;
namespace ImmichLoungeCompanion.Tests.Playlist;
[TestClass]
public class SmartSearchSourceTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SearchIntersectsSharedAlbumsAndHonorsFiltersAndExclusions(bool exclude)
    {
        var settings = new ImmichSettings();
        var client = Substitute.For<IImmichClient>();
        client.GetSearchApiCapabilitiesAsync(settings).Returns(new SearchApiCapabilities("3.3.1", true));
        var requests = new List<SearchAssetsRequest>();
        client.SmartSearchAsync(settings, "beach", Arg.Any<SearchAssetsRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<SearchAssetsRequest>();
            requests.Add(request);
            return request.AlbumIds == null ? new List<ImmichAsset> { new() { Id = "owned" } }
                : [new() { Id = "shared", Type = "IMAGE", ExifInfo = new() { Country = "Switzerland", FileSizeInByte = 10000 } }];
        });
        client.SearchAssetsAllPagesAsync(settings, Arg.Any<SearchAssetsRequest>()).Returns(call =>
        {
            var request = call.Arg<SearchAssetsRequest>();
            return request.PersonIds != null ? new List<ImmichAsset> { new() { Id = "shared" } }
                : [new() { Id = "shared", Type = "IMAGE", ExifInfo = new() { Country = "Switzerland", FileSizeInByte = 10000 } }, new() { Id = "album-only" }];
        });
        var profile = new Profile { PhotoSource = "selected", OnlyPhotosWithPeople = true,
            ContentSources = [new() { Type = "album", Id = "album" }, new() { Type = "search", Id = "beach", Label = "Beach" }],
            DateFilter = new() { Type = "range", From = "2024-01-01" }, LocationFilter = new() { Country = "Switzerland" },
            Quality = new() { MinFileSizeKb = 1 } };
        profile.AssetFilter = ProfileAssetFilterExtensions.BuildAssetFilterFromContentSources(profile.ContentSources, AssetFilterGroupOperator.And);
        if (exclude) { profile.ExcludedPeople = [new() { Type = "person", Id = "blocked" }]; }
        var assets = await new PlaylistAssetCollector(client).CollectAsync(profile, settings);
        Assert.AreEqual(exclude ? 0 : 1, assets.Count);
        if (!exclude) { Assert.IsTrue(assets.ContainsKey("shared")); }
        Assert.AreEqual(2, requests.Count);
        Assert.IsNull(requests[0].AlbumIds);
        CollectionAssert.AreEqual(new[] { "album" }, requests[1].AlbumIds!);
        foreach (var request in requests)
        {
            Assert.IsTrue(request.HasPeople);
            Assert.AreEqual("IMAGE", request.Type);
            Assert.AreEqual("Switzerland", request.Country);
            Assert.AreEqual("2024-01-01", request.TakenAfter);
        }
    }
    [TestMethod]
    public void LegacySearchOnlySourceRemainsEditableAndSelected()
    {
        var profile = new Profile { ContentSources = [new() { Type = "search", Id = "beach" }] };
        Assert.AreEqual("selected", profile.EffectivePhotoSource());
        profile.NormalizeAssetFilter();
        Assert.IsTrue(profile.CanEditContentSources());
        Assert.AreEqual(AssetFilterConditionType.Search, profile.AssetFilter!.Children.Single().Type);
    }
}
