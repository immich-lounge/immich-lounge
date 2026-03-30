using System.Threading.Tasks;
using ImmichLoungeCompanion.Models;

namespace ImmichLoungeCompanion.Services;

public interface IProfileDocumentService
{
    Profile Normalize(Profile profile);
    Task<object> BuildResponseAsync(Profile profile);
}
