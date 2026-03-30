using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Services;

public interface IProfileValidator
{
    string? Validate(Profile profile);
}
