
using ApexPost.Infrastructure.Persistence;

namespace ApexPost.Application.Tests.Persistence;

internal sealed class JsonRepositoryTestDirectory : IDisposable
{
    public string Path { get; } =
        Directory.CreateTempSubdirectory("ApexPostRepositoryTests").FullName;

    public JsonPersistenceOptions CreateOptions() =>
        new() { DataDirectory = Path };

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
