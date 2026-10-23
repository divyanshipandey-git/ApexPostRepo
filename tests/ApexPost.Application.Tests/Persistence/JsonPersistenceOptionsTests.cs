
using ApexPost.Infrastructure.Persistence;
using Xunit;

namespace ApexPost.Application.Tests.Persistence;

public sealed class JsonPersistenceOptionsTests
{
    [Fact]
    public void GetFilePath_CombinesDataDirectoryAndFileName()
    {
        var options = new JsonPersistenceOptions
        {
            DataDirectory = Path.Combine(
                Path.GetTempPath(),
                "ApexPostTests")
        };

        var result = options.GetFilePath("products.json");

        Assert.Equal(
            Path.GetFullPath(
                Path.Combine(options.DataDirectory, "products.json")),
            result);
    }

    [Fact]
    public void GetFilePath_RejectsEmptyFileName()
    {
        var options = new JsonPersistenceOptions();

        Assert.Throws<ArgumentException>(
            () => options.GetFilePath(""));
    }

    [Fact]
    public void GetFilePath_RejectsDirectoryTraversal()
    {
        var options = new JsonPersistenceOptions
        {
            DataDirectory = Path.GetTempPath()
        };

        Assert.Throws<ArgumentException>(
            () => options.GetFilePath("../products.json"));
    }

    [Fact]
    public void GetFilePath_RejectsWhitespaceDataDirectory()
    {
        var options = new JsonPersistenceOptions
        {
            DataDirectory = " "
        };

        Assert.Throws<ArgumentException>(
            () => options.GetFilePath("products.json"));
    }
}
