namespace ImmichLoungeCompanion.Tests.Helpers;

public sealed class TempDataDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());

    public TempDataDirectory() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
