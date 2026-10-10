namespace ImmichLoungeCompanion.Immich.Dtos;

public sealed record SearchApiCapabilities(string? ServerVersion, bool StructuredSearch, string? Warning = null)
{
    public bool BirthdayMemories => System.Version.TryParse(ServerVersion, out var version) &&
        version >= new System.Version(3, 3, 0);
}
