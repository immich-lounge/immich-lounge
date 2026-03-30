using System;
using ImmichLoungeCompanion.Components;
using ImmichLoungeCompanion.Playlist;
using ImmichLoungeCompanion.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var dataDir = builder.Configuration["DataDirectory"] ?? "/data";

// Storage
builder.Services.AddSingleton<ISettingsRepository>(_ => new JsonSettingsRepository(dataDir));
builder.Services.AddSingleton<IProfileRepository>(_ => new JsonProfileRepository(dataDir));

// Playlist cache + builder + background worker
builder.Services.AddSingleton<IPlaylistCache>(
    _ => new ImmichLoungeCompanion.Playlist.PlaylistCache(dataDir));
builder.Services.AddSingleton<ImmichLoungeCompanion.Playlist.PlaylistAssetCollector>();
builder.Services.AddSingleton<ImmichLoungeCompanion.Playlist.IPlaylistBuilder,
    ImmichLoungeCompanion.Playlist.PlaylistBuilder>();
builder.Services.AddSingleton<ImmichLoungeCompanion.Playlist.PlaylistCacheWorker>();
builder.Services.AddHostedService(sp =>
    sp.GetRequiredService<ImmichLoungeCompanion.Playlist.PlaylistCacheWorker>());

// HTTP client for Immich calls
builder.Services.AddHttpClient("immich");

// HTTP client for Nominatim geocoding (User-Agent required by ToS)
builder.Services.AddHttpClient("nominatim", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("immich-lounge/1.0");
});

// Named client for Blazor components to call the local REST API.
// ASPNETCORE_URLS is set by launchSettings.applicationUrl in dev and by
// ENV ASPNETCORE_URLS in Docker. Fall back to ASPNETCORE_HTTP_PORTS, then 4383.
builder.Services.AddHttpClient("local", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var urls = config["ASPNETCORE_URLS"];
    if (urls != null)
    {
        var first = urls.Split(';')[0].Replace("*", "localhost").Replace("+", "localhost");
        client.BaseAddress = new Uri(first);
    }
    else
    {
        var port = config["ASPNETCORE_HTTP_PORTS"] ?? "4383";
        client.BaseAddress = new Uri($"http://localhost:{port}");
    }
});

// ImmichClient is stateless (uses IHttpClientFactory per call) — Singleton is correct.
// Do NOT use AddScoped; PlaylistCacheWorker (Singleton) depends on IImmichClient,
// and a Singleton cannot capture a Scoped service without causing a captive dependency error.
builder.Services.AddSingleton<ImmichLoungeCompanion.Immich.IImmichClient,
    ImmichLoungeCompanion.Immich.ImmichClient>();

// Scoped UI state
builder.Services.AddScoped<ImmichLoungeCompanion.Services.CompanionState>();
builder.Services.AddSingleton<ImmichLoungeCompanion.Services.IDisplayDateFormattingService,
    ImmichLoungeCompanion.Services.DisplayDateFormattingService>();
builder.Services.AddSingleton<ImmichLoungeCompanion.Services.IProfileValidator,
    ImmichLoungeCompanion.Services.ProfileValidator>();
builder.Services.AddSingleton<ImmichLoungeCompanion.Services.IProfileDocumentService,
    ImmichLoungeCompanion.Services.ProfileDocumentService>();

// Controllers + Blazor
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Make Program accessible to WebApplicationFactory in tests
public partial class Program { }
