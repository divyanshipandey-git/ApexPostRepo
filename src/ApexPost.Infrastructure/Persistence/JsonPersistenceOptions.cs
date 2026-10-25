
namespace ApexPost.Infrastructure.Persistence;

public sealed class JsonPersistenceOptions
{
    public string DataDirectory { get; set; } = ResolveDataDirectory("App_Data");

    public static string ResolveDataDirectory(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        if (Path.IsPathRooted(dataDirectory))
        {
            return Path.GetFullPath(dataDirectory);
        }

        return Path.GetFullPath(Path.Combine(FindSolutionRootDirectory(), dataDirectory));
    }

    public static string FindSolutionRootDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ApexPost.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(AppContext.BaseDirectory);
    }

    public string GetFilePath(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (!string.Equals(
                fileName,
                Path.GetFileName(fileName),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A data file name must not contain a directory path.",
                nameof(fileName));
        }

        if (fileName is "." or "..")
        {
            throw new ArgumentException(
                "The data file name is invalid.",
                nameof(fileName));
        }

        return Path.Combine(
            ResolveDataDirectory(DataDirectory),
            fileName);
    }
}
