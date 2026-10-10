namespace ImmichLoungeCompanion.Immich.Dtos;

public sealed record SearchApiCapabilities(string? ServerVersion, bool StructuredSearch, string? Warning = null);