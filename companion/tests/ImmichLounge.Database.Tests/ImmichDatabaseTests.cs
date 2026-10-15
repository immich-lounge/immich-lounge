using System.Text;
using DotNet.Testcontainers.Builders;
using ImmichLounge.LiveImmich;

[assembly: DoNotParallelize]

namespace ImmichLounge.Database.Tests;

[TestClass]
[TestCategory("Integration")]
public sealed class ImmichDatabaseTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("v31", "3.1.0", "b434cb9287eea1471c9974845914d4dd328c9c2d652e446ed4930f99944f0ceb")]
    [DataRow("compat-v32", "3.2.0", "ae13784ffcfcce8f4178113eb6661602a1fd1912f3d539880b8ac0dd95fc8ac2")]
    [DataRow("development", "3.3.1", "db996e352359771c6a3db121a0ed8761516b22f727fc092a0f46dcfef82c1bc1")]
    public async Task SearchAndPlaylistContractsAgainstRealImmich(string name, string version, string digest)
    {
        var guard = $"lounge-test-{Guid.NewGuid():N}";
        var password = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(TestContext.TestResultsDirectory!, guard);
        Directory.CreateDirectory(directory);

        await using var network = new NetworkBuilder().Build();
        await using var databaseVolume = new VolumeBuilder().Build();
        await using var libraryVolume = new VolumeBuilder().Build();
        await using var database = new ContainerBuilder(
                "ghcr.io/immich-app/postgres:14-vectorchord0.4.3-pgvectors0.2.0@sha256:bcf63357191b76a916ae5eb93464d65c07511da41e3bf7a8416db519b40b1c23")
            .WithNetwork(network).WithNetworkAliases("database")
            .WithEnvironment("POSTGRES_PASSWORD", password)
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithEnvironment("POSTGRES_DB", "immich")
            .WithEnvironment("POSTGRES_INITDB_ARGS", "--data-checksums")
            .WithVolumeMount(databaseVolume, "/var/lib/postgresql/data")
            .WithCreateParameterModifier(parameters => parameters.HostConfig!.ShmSize = 128 * 1024 * 1024)
            .WithResourceMapping(Encoding.UTF8.GetBytes($"""
                CREATE SCHEMA immich_lounge_fixture;
                CREATE TABLE immich_lounge_fixture.guard (name text PRIMARY KEY);
                INSERT INTO immich_lounge_fixture.guard VALUES ('{guard}');
                """), "/docker-entrypoint-initdb.d/99-lounge-fixture.sql")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilExternalTcpPortIsAvailable(5432, options => options.WithTimeout(TimeSpan.FromMinutes(2))))
            .Build();
        await using var redis = new ContainerBuilder(
                "docker.io/valkey/valkey:9@sha256:70739f85ad2ee01a726a965584a0f94895f01b0c60b3cc8b0aeef11eaa6888cf")
            .WithNetwork(network).WithNetworkAliases("redis")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilInternalTcpPortIsAvailable(6379, options => options.WithTimeout(TimeSpan.FromMinutes(2))))
            .Build();
        await using var server = new ContainerBuilder($"ghcr.io/immich-app/immich-server:v{version}@sha256:{digest}")
            .WithNetwork(network)
            .WithEnvironment("DB_HOSTNAME", "database")
            .WithEnvironment("DB_USERNAME", "postgres")
            .WithEnvironment("DB_PASSWORD", password)
            .WithEnvironment("DB_DATABASE_NAME", "immich")
            .WithEnvironment("REDIS_HOSTNAME", "redis")
            .WithEnvironment("IMMICH_CONFIG_FILE", "/config/immich.json")
            .WithResourceMapping(Encoding.UTF8.GetBytes(
                "{\"machineLearning\":{\"enabled\":false},\"reverseGeocoding\":{\"enabled\":false}}"), "/config/immich.json")
            .WithVolumeMount(libraryVolume, "/data")
            .WithPortBinding(2283, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
                .ForPort(2283).ForPath("/api/server/version"), options => options.WithTimeout(TimeSpan.FromMinutes(3))))
            .Build();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(8));
            await network.CreateAsync(timeout.Token);
            await databaseVolume.CreateAsync(timeout.Token);
            await libraryVolume.CreateAsync(timeout.Token);
            await Task.WhenAll(database.StartAsync(timeout.Token), redis.StartAsync(timeout.Token));
            await server.StartAsync(timeout.Token);
            var endpoint = new UriBuilder("http", server.Hostname, server.GetMappedPublicPort(2283)).Uri;
            await FixtureRunner.RunAsync(["seed", name, directory], endpoint);
            var marker = await database.ExecAsync(["psql", "-U", "postgres", "-d", "immich", "-Atc",
                "SELECT name FROM immich_lounge_fixture.guard"]);
            Assert.AreEqual(0, marker.ExitCode, "Fixture database marker must be readable.");
            Assert.AreEqual(guard, marker.Stdout.Trim(), "Refusing SQL writes to a database without this test's marker.");
            var sql = await File.ReadAllTextAsync(Path.Combine(directory, $"{name}-fixtures.sql"));
            var seeded = await database.ExecAsync(["psql", "-U", "postgres", "-d", "immich", "-v", "ON_ERROR_STOP=1", "-c", sql]);
            Assert.AreEqual(0, seeded.ExitCode, "Could not apply synthetic fixture metadata.");
            await FixtureRunner.RunAsync(["test", name, directory], endpoint);
            TestContext.AddResultFile(Path.Combine(directory, $"{name}-results.json"));
        }
        catch
        {
            // Attach service logs on failure; never attach account files or database credentials.
            foreach (var (service, container) in new[] { ("database", database), ("redis", redis), ("server", server) })
            {
                try
                {
                    var (stdout, stderr) = await container.GetLogsAsync();
                    var log = Path.Combine(directory, $"{name}-{service}.log");
                    var text = (stdout + stderr).Replace(password, "[REDACTED]");
                    var accountPath = Path.Combine(directory, $"{name}-account.json");
                    if (File.Exists(accountPath))
                    {
                        using var account = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(accountPath));
                        foreach (var key in new[] { "password", "adminPassword", "apiKey" })
                        {
                            if (account.RootElement.TryGetProperty(key, out var value) && value.GetString() is { Length: > 0 } secret)
                            {
                                text = text.Replace(secret, "[REDACTED]");
                            }
                        }
                    }
                    await File.WriteAllTextAsync(log, text);
                    TestContext.AddResultFile(log);
                }
                catch (Exception error)
                {
                    TestContext.WriteLine($"Could not collect {service} logs: {error.GetType().Name}");
                }
            }
            throw;
        }
        finally
        {
            var account = Path.Combine(directory, $"{name}-account.json");
            if (File.Exists(account))
            {
                File.Delete(account);
            }
        }
    }
}
