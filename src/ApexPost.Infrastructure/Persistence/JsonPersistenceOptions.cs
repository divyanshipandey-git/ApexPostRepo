
namespace ApexPost.Infrastructure.Persistence;

public sealed class JsonPersistenceOptions
{
    public string DataDirectory { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "App_Data");

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
            Path.GetFullPath(DataDirectory),
            fileName);
    }
}
