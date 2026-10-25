
using ApexPost.Infrastructure.Persistence;
using Xunit;

namespace ApexPost.Application.Tests.Persistence;

public sealed class JsonPersistenceOptionsTests
{
    [Fact]
    public void DefaultDataDirectory_IsAtDiscoveredSolutionRoot()
    {
        var solutionRoot = JsonPersistenceOptions.FindSolutionRootDirectory();
        var options = new JsonPersistenceOptions();

        Assert.True(File.Exists(Path.Combine(solutionRoot, "ApexPost.sln")));
        Assert.Equal(Path.Combine(solutionRoot, "App_Data"), options.DataDirectory);
    }

    [Fact]
    public void RelativeDataDirectory_IsResolvedFromSolutionRoot()
    {
        var expected = Path.Combine(JsonPersistenceOptions.FindSolutionRootDirectory(), "local-data");

        var result = JsonPersistenceOptions.ResolveDataDirectory("local-data");

        Assert.Equal(expected, result);
    }

    [Fact]
    public void AbsoluteDataDirectory_RemainsAnExplicitOverride()
    {
        var configured = Path.Combine(Path.GetTempPath(), "ApexPostConfiguredData");

        var result = JsonPersistenceOptions.ResolveDataDirectory(configured);

        Assert.Equal(Path.GetFullPath(configured), result);
    }

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
