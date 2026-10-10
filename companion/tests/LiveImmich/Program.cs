using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

if (args.Length != 3 || args[0] != "seed" || args[1] is not ("v31" or "v32"))
{
    throw new ArgumentException("Usage: LiveImmich seed <v31|v32> <output-directory>");
}
var stack = args[1];
var baseUrl = stack == "v31" ? "http://127.0.0.1:12283" : "http://127.0.0.1:13283";
var expectedMinor = stack == "v31" ? 1 : 2;
var output = Path.GetFullPath(args[2]);
Directory.CreateDirectory(output);
var statePath = Path.Combine(output, $"{stack}-account.json");
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };
var version = await http.GetFromJsonAsync<JsonElement>("/api/server/version");
Require(version.GetProperty("major").GetInt32() == 3 && version.GetProperty("minor").GetInt32() == expectedMinor,
    "This runner only writes to the dedicated pinned local test servers.");
var state = File.Exists(statePath)
    ? JsonSerializer.Deserialize<TestState>(await File.ReadAllTextAsync(statePath), jsonOptions)!
    : new TestState { Password = Guid.NewGuid().ToString("N") + "Aa1!", AdminPassword = Guid.NewGuid().ToString("N") + "Aa1!" };

if (args[0] == "seed")
{
    // Credentials never go to console output or Git.
    await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(state, jsonOptions));
    if (state.Assets.Count == 4 && state.AlbumA.Length > 0 && state.AlbumB.Length > 0)
    {
        Console.WriteLine($"{stack}: reusing persistent test account and fixtures.");
    }
    using var signup = await http.PostAsJsonAsync("/api/auth/admin-sign-up",
        new { email = state.AdminEmail, password = state.AdminPassword, name = "Immich Lounge Development" });
    if (!signup.IsSuccessStatusCode && signup.StatusCode != HttpStatusCode.BadRequest)
    {
        throw new InvalidOperationException($"Test admin signup failed: HTTP {(int)signup.StatusCode}.");
    }
    var adminLogin = await SendJson(HttpMethod.Post, "/api/auth/login", new { email = state.AdminEmail, password = state.AdminPassword });
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminLogin.GetProperty("accessToken").GetString());
    if (state.ApiKey.Length == 0)
    {
        using var created = await http.PostAsJsonAsync("/api/admin/users",
            new { email = state.Email, password = state.Password, name = "Immich Lounge Fixtures", shouldChangePassword = false });
        Require(created.IsSuccessStatusCode || created.StatusCode == HttpStatusCode.BadRequest,
            $"Fixture user creation failed: HTTP {(int)created.StatusCode}.");
    }
    var login = await SendJson(HttpMethod.Post, "/api/auth/login", new { email = state.Email, password = state.Password });
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
    if (state.ApiKey.Length == 0)
    {
        var key = await SendJson(HttpMethod.Post, "/api/api-keys", new
        {
            name = "Immich Lounge live tests",
            permissions = new[] { "asset.read", "asset.statistics", "asset.view", "album.read", "person.read", "tag.read", "memory.read" }
        });
        state.ApiKey = key.GetProperty("secret").GetString()!;
        await SaveState();
    }

    var dates = new[] { "2020-01-01T00:00:00.000Z", "2020-06-15T12:00:00.000Z", "2021-01-01T00:00:00.000Z", "2022-01-01T00:00:00.000Z" };
    var countries = new string?[] { "Switzerland", "Switzerland", "Canada", null };
    var regions = new string?[] { "Zürich", "Bern", "British Columbia", null };
    var cities = new string?[] { "Zürich", "Bern", "Vancouver", null };
    for (var i = state.Assets.Count; i < 4; i++)
    {
        using var form = new MultipartFormDataContent();
        var image = new ByteArrayContent(CreatePng(i));
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(image, "assetData", $"lounge-fixture-{i}.png");
        form.Add(new StringContent(dates[i]), "fileCreatedAt");
        form.Add(new StringContent(dates[i]), "fileModifiedAt");
        using var uploaded = await http.PostAsync("/api/assets", form);
        uploaded.EnsureSuccessStatusCode();
        var result = await uploaded.Content.ReadFromJsonAsync<JsonElement>();
        state.Assets.Add(new Fixture(result.GetProperty("id").GetString()!, dates[i], countries[i], regions[i], cities[i]));
        await SaveState();
    }
    if (state.AlbumA.Length == 0)
    {
        var album = await SendJson(HttpMethod.Post, "/api/albums", new
        {
            albumName = "Lounge fixture A", assetIds = state.Assets.Take(2).Select(x => x.Id).ToArray()
        });
        state.AlbumA = album.GetProperty("id").GetString()!;
        await SaveState();
    }
    if (state.AlbumB.Length == 0)
    {
        var album = await SendJson(HttpMethod.Post, "/api/albums", new
        {
            albumName = "Lounge fixture B", assetIds = state.Assets.Skip(1).Take(2).Select(x => x.Id).ToArray()
        });
        state.AlbumB = album.GetProperty("id").GetString()!;
        await SaveState();
    }

    // Wait for actual upload metadata extraction before setting deterministic search fixtures.
    var deadline = DateTime.UtcNow.AddMinutes(2);
    foreach (var fixture in state.Assets)
    {
        while (true)
        {
            var asset = await http.GetFromJsonAsync<JsonElement>($"/api/assets/{fixture.Id}");
            if (asset.TryGetProperty("exifInfo", out var exif) && exif.ValueKind == JsonValueKind.Object &&
                exif.TryGetProperty("exifImageWidth", out var width) && width.ValueKind == JsonValueKind.Number)
            {
                break;
            }
            Require(DateTime.UtcNow < deadline, "Timed out waiting for uploaded fixture metadata.");
            await Task.Delay(500);
        }
    }
    var sql = new StringBuilder().AppendLine("BEGIN;");
    foreach (var asset in state.Assets)
    {
        Require(Guid.TryParse(asset.Id, out _), "Invalid fixture asset ID.");
        sql.AppendLine($"UPDATE asset_exif SET country={Sql(asset.Country)}, state={Sql(asset.State)}, city={Sql(asset.City)} WHERE \"assetId\"='{asset.Id}';");
        sql.AppendLine($"UPDATE asset SET \"fileCreatedAt\"='{asset.Date}' WHERE id='{asset.Id}';");
    }
    sql.AppendLine("COMMIT;");
    await File.WriteAllTextAsync(Path.Combine(output, $"{stack}-fixtures.sql"), sql.ToString(), new UTF8Encoding(false));
    Console.WriteLine($"{stack}: four synthetic photo fixtures and two overlapping albums are ready.");
    return;
}

async Task<JsonElement> SendJson(HttpMethod method, string path, object body)
{
    using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
    using var response = await http.SendAsync(request);
    Require(response.IsSuccessStatusCode, $"Test setup request {path} failed: HTTP {(int)response.StatusCode}.");
    return await response.Content.ReadFromJsonAsync<JsonElement>();
}
Task SaveState() => File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(state, jsonOptions));
static string Sql(string? value) => value == null ? "NULL" : "'" + value.Replace("'", "''") + "'";
static void Require(bool condition, string message)
{
    if (!condition) { throw new InvalidOperationException(message); }
}

// Synthetic code-generated PNGs: no real library photos or external image downloads.
static byte[] CreatePng(int variant)
{
    using var output = new MemoryStream();
    output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    var header = new byte[13];
    BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), 96);
    BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), 96);
    header[8] = 8; header[9] = 2;
    Chunk("IHDR", header);
    using var compressed = new MemoryStream();
    using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
    {
        for (var y = 0; y < 96; y++)
        {
            zlib.WriteByte(0);
            for (var x = 0; x < 96; x++)
            {
                zlib.WriteByte((byte)(x + variant * 32));
                zlib.WriteByte((byte)(y + variant * 16));
                zlib.WriteByte((byte)(x + y + variant * 8));
            }
        }
    }
    Chunk("IDAT", compressed.ToArray());
    Chunk("IEND", []);
    return output.ToArray();

    void Chunk(string type, byte[] bytes)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        output.Write(length);
        var data = Encoding.ASCII.GetBytes(type).Concat(bytes).ToArray();
        output.Write(data);
        uint crc = 0xffffffff;
        foreach (var item in data)
        {
            crc ^= item;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) == 1 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
            }
        }
        BinaryPrimitives.WriteUInt32BigEndian(length, crc ^ 0xffffffff);
        output.Write(length);
    }
}

sealed class TestState
{
    public string AdminEmail { get; set; } = "development@immich-lounge.test";
    public string AdminPassword { get; set; } = "";
    public string Email { get; set; } = "fixtures@immich-lounge.test";
    public string Password { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string AlbumA { get; set; } = "";
    public string AlbumB { get; set; } = "";
    public List<Fixture> Assets { get; set; } = [];
}
sealed record Fixture(string Id, string Date, string? Country, string? State, string? City);
