using n2jSoft.ModularGuard.CLI.Configuration;
using n2jSoft.ModularGuard.CLI.Models;
using n2jSoft.ModularGuard.CLI.Services;
using n2jSoft.ModularGuard.CLI.Validation;

namespace n2jSoft.ModularGuard.CLI.Tests.Integration;

[Collection("Assembly")]
public class EdgeCaseIntegrationTests
{
    private readonly string _fixturePath;

    public EdgeCaseIntegrationTests()
    {
        _fixturePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Fixtures",
            "EdgeCases");
    }

    [Fact]
    public void EmptyProject_ShouldLoadSuccessfully()
    {
        // Arrange
        var emptyProjectPath = Path.Combine(_fixturePath, "EmptyProject", "Empty.Core.csproj");

        // Act
        using var loaderService = new ProjectLoaderService();
        var projectInfo = loaderService.LoadProject(emptyProjectPath);

        // Assert
        Assert.NotNull(projectInfo);
        Assert.Equal("Empty.Core", projectInfo.Name);
        Assert.Empty(projectInfo.ProjectReferences);
    }

    [Fact]
    public void EmptyDirectory_ShouldReturnNoProjects()
    {
        // Arrange
        var emptyDir = Path.Combine(_fixturePath, "EmptyDirectory");
        Directory.CreateDirectory(emptyDir);

        var discoveryService = new ProjectDiscoveryService();

        // Act
        var projects = discoveryService.DiscoverProjects(emptyDir).ToList();

        // Assert
        Assert.Empty(projects);

        // Cleanup
        Directory.Delete(emptyDir);
    }

    [Fact]
    public void MalformedProject_ShouldThrowException()
    {
        // Arrange
        var malformedProjectPath = Path.Combine(_fixturePath, "MalformedProject", "Malformed.Core.csproj");

        // Act & Assert
        using var loaderService = new ProjectLoaderService();
        Assert.Throws<InvalidOperationException>(() => loaderService.LoadProject(malformedProjectPath));
    }

    [Fact]
    public void NonExistentDirectory_ShouldReturnNoProjects()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_fixturePath, "NonExistent");
        var discoveryService = new ProjectDiscoveryService();

        // Act
        var projects = discoveryService.DiscoverProjects(nonExistentPath).ToList();

        // Assert
        Assert.Empty(projects);
    }

    [Fact]
    public void UnknownProjectType_ShouldProduceViolation()
    {
        // Arrange
        var configLoader = new ConfigurationLoader();
        var configuration = configLoader.TryLoadConfiguration(_fixturePath)
                            ?? configLoader.CreateDefaultConfiguration();

        var unknownProjectPath = Path.Combine(_fixturePath, "UnknownProject", "RandomProject.csproj");

        using var loaderService = new ProjectLoaderService();
        var projectInfo = loaderService.LoadProject(unknownProjectPath);

        var typeDetector = new ConfigurableProjectTypeDetector(configuration);
        var projectType = typeDetector.DetectProjectType(projectInfo.Name, unknownProjectPath, _fixturePath);
        var moduleName = typeDetector.ExtractModuleName(projectInfo.Name, projectType);

        var modules = new List<ModuleInfo>
        {
            new(moduleName, projectType, projectInfo)
        };

        // Act
        var validationEngine = new ConfigurableValidationEngine(configuration);
        var result = validationEngine.ValidateAllWithResult(modules);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Violations);

        var unknownProjectViolation = result.Violations.FirstOrDefault(v =>
            v.ProjectName == "RandomProject" &&
            v.RuleName == "UnknownProjectTypeRule");

        Assert.NotNull(unknownProjectViolation);
        Assert.NotNull(unknownProjectViolation.Suggestion);
        Assert.Contains("ignoredProjects", unknownProjectViolation.Suggestion);
    }

    [Fact]
    public void ProjectWithNonExistentReference_ShouldLoadWithoutCrashing()
    {
        // Arrange
        var projectPath = Path.Combine(_fixturePath, "EmptyProject", "Empty.Core.csproj");

        // Act
        using var loaderService = new ProjectLoaderService();
        var projectInfo = loaderService.LoadProject(projectPath);

        // Assert - Should not throw exception even if references don't exist
        Assert.NotNull(projectInfo);
    }

    [Fact]
    public void NestedDirectoryStructure_ShouldDiscoverAllProjects()
    {
        // Arrange
        var discoveryService = new ProjectDiscoveryService();

        // Act
        var projects = discoveryService.DiscoverProjects(_fixturePath).ToList();

        // Assert
        Assert.NotEmpty(projects);
        Assert.True(projects.Count >= 3, $"Expected at least 3 projects in edge cases, found {projects.Count}");
    }
}