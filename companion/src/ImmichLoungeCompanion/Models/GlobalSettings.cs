using System;

namespace ImmichLoungeCompanion.Models;

public class GlobalSettings
{
    public int SchemaVersion { get; set; } = 1;
    public string FriendlyName { get; set; } = "Home";
    public ImmichSettings Immich { get; set; } = new();
    public string CompanionUuid { get; set; } = Guid.NewGuid().ToString();
}

public class ImmichSettings
{
    public string ServerUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";

    public override bool Equals(object? obj) =>
        obj is ImmichSettings other &&
        ServerUrl == other.ServerUrl &&
        ApiKey == other.ApiKey;

    public override int GetHashCode() => HashCode.Combine(ServerUrl, ApiKey);
}
