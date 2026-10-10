using System.Net;
using System.Reflection;
using ImmichLoungeCompanion.Components.Shared;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.App;

[TestClass]
public sealed class SourcePickerTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestMethod]
    [DataRow("http")]
    [DataRow("json")]
    [DataRow("timeout")]
    public async Task FailedSourceLoadKeepsComponentUsableAndCanBeRetried(string failure)
    {
        using var handler = new SourceHandler(failure);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("local").Returns(client);
        var picker = new SourcePicker();
        typeof(SourcePicker).GetProperty("HttpFactory", PrivateInstance)!.SetValue(picker, factory);

        await LoadAsync(picker);
        Assert.IsNotNull(GetField(picker, "_loadError"));
        Assert.AreEqual(false, GetField(picker, "_loading"));
        Assert.AreEqual(false, GetField(picker, "_showDropdown"));

        handler.Fail = false;
        await LoadAsync(picker);
        Assert.IsNull(GetField(picker, "_loadError"));
        Assert.AreEqual(false, GetField(picker, "_loading"));
        Assert.AreEqual(1, ((System.Collections.ICollection)GetField(picker, "_items")!).Count);
    }

    private static Task LoadAsync(SourcePicker picker) =>
        (Task)typeof(SourcePicker).GetMethod("LoadItemsAsync", PrivateInstance)!.Invoke(picker, null)!;

    private static object? GetField(SourcePicker picker, string name) =>
        typeof(SourcePicker).GetField(name, PrivateInstance)!.GetValue(picker);

    private sealed class SourceHandler(string failure) : HttpMessageHandler
    {
        public bool Fail { get; set; } = true;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Fail && failure == "timeout") { throw new TaskCanceledException(); }
            return Task.FromResult(new HttpResponseMessage(Fail && failure == "http" ? HttpStatusCode.InternalServerError : HttpStatusCode.OK)
            {
                Content = new StringContent(Fail ? "not json" : "[{\"id\":\"album-one\",\"name\":\"Holiday\"}]")
            });
        }
    }
}
