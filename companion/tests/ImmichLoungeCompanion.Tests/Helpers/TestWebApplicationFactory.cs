using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.Helpers;

public class TestWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly TempDataDirectory _dir = new();

    public string DataDirectory => _dir.Path;

    /// <summary>
    /// Preconfigured mock Immich client. Tests can set up return values on this before making requests.
    /// Defaults to returning empty lists for all calls (no real HTTP calls made).
    /// </summary>
    public IImmichClient ImmichClient { get; } = CreateDefaultImmichMock();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DataDirectory", _dir.Path);

        builder.ConfigureServices(services =>
        {
            // Replace the real ImmichClient with a mock so tests never call real Immich
            services.RemoveAll<IImmichClient>();
            services.AddSingleton(ImmichClient);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _dir.Dispose();
        }
    }

    private static IImmichClient CreateDefaultImmichMock()
    {
        var mock = Substitute.For<IImmichClient>();
        mock.TestConnectionAsync(Arg.Any<ImmichSettings>())
            .Returns((true, 0, 0, (string?)null));
        mock.GetAlbumsAsync(Arg.Any<ImmichSettings>())
            .Returns(new List<ImmichAlbum>());
        mock.GetPeopleAsync(Arg.Any<ImmichSettings>())
            .Returns(new List<ImmichPerson>());
        mock.GetTagsAsync(Arg.Any<ImmichSettings>())
            .Returns(new List<ImmichTag>());
        mock.SearchAssetsAllPagesAsync(Arg.Any<ImmichSettings>(), Arg.Any<SearchAssetsRequest>())
            .Returns(new List<ImmichAsset>());
        mock.GetMemoriesAsync(Arg.Any<ImmichSettings>(), Arg.Any<DateOnly>())
            .Returns(new List<ImmichMemory>());
        return mock;
    }
}
