using n2jSoft.ModularGuard.CLI.Configuration;
using n2jSoft.ModularGuard.CLI.Models;
using n2jSoft.ModularGuard.CLI.Services;
using n2jSoft.ModularGuard.CLI.Validation;

namespace n2jSoft.ModularGuard.CLI.Tests.Integration;

[Collection("Assembly")]
public class ValidStructureIntegrationTests
{
    private readonly string _fixturePath;

    public ValidStructureIntegrationTests()
    {
        _fixturePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Fixtures",
            "ValidStructure");
    }

    [Fact]
    public void ValidStructure_ShouldHaveNoViolations()
    {
        // Arrange
        var configLoader = new ConfigurationLoader();
        var configuration = configLoader.TryLoadConfiguration(_fixturePath)
                            ?? configLoader.CreateDefaultConfiguration();

        var discoveryService = new ProjectDiscoveryService();
        var projectPaths = discoveryService.DiscoverProjects(_fixturePath).ToList();

        var modules = new List<ModuleInfo>();
        var typeDetector = new ConfigurableProjectTypeDetector(configuration);

        using var loaderService = new ProjectLoaderService();
        foreach (var projectPath in projectPaths)
        {
            var projectInfo = loaderService.LoadProject(projectPath);
            var projectType = typeDetector.DetectProjectType(projectInfo.Name, projectPath, _fixturePath);
            var moduleName = typeDetector.ExtractModuleName(projectInfo.Name, projectType);
            modules.Add(new ModuleInfo(moduleName, projectType, projectInfo));
        }

        // Act
        var validationEngine = new ConfigurableValidationEngine(configuration);
        var result = validationEngine.ValidateAllWithResult(modules);

        // Assert
        Assert.True(result.IsValid, "Valid structure should have no violations");
        Assert.Empty(result.Violations);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
    }

    [Fact]
    public void ValidStructure_ShouldLoadAllProjects()
    {
        // Arrange
        var discoveryService = new ProjectDiscoveryService();

        // Act
        var projectPaths = discoveryService.DiscoverProjects(_fixturePath).ToList();

        // Assert
        Assert.True(projectPaths.Count >= 7, $"Expected at least 7 projects, found {projectPaths.Count}");
        Assert.Contains(projectPaths, p => p.Contains("Users.Core.csproj"));
        Assert.Contains(projectPaths, p => p.Contains("Users.Infrastructure.csproj"));
        Assert.Contains(projectPaths, p => p.Contains("Users.Private.App.csproj"));
        Assert.Contains(projectPaths, p => p.Contains("Users.Private.Endpoints.csproj"));
    }

    [Fact]
    public void ValidStructure_ShouldDetectCorrectProjectTypes()
    {
        // Arrange
        var configLoader = new ConfigurationLoader();
        var configuration = configLoader.TryLoadConfiguration(_fixturePath)
                            ?? configLoader.CreateDefaultConfiguration();

        var typeDetector = new ConfigurableProjectTypeDetector(configuration);

        // Act & Assert
        Assert.Equal("core", typeDetector.DetectProjectType("Users.Core"));
        Assert.Equal("infrastructure", typeDetector.DetectProjectType("Users.Infrastructure"));
        Assert.Equal("private-app", typeDetector.DetectProjectType("Users.Private.App"));
        Assert.Equal("private-endpoints", typeDetector.DetectProjectType("Users.Private.Endpoints"));
        Assert.Equal("shared-core", typeDetector.DetectProjectType("Shared.Core"));
    }

    [Fact]
    public void ValidStructure_ShouldExtractCorrectModuleNames()
    {
        // Arrange
        var configLoader = new ConfigurationLoader();
        var configuration = configLoader.TryLoadConfiguration(_fixturePath)
                            ?? configLoader.CreateDefaultConfiguration();

        var typeDetector = new ConfigurableProjectTypeDetector(configuration);

        // Act & Assert
        Assert.Equal("Users", typeDetector.ExtractModuleName("Users.Core", "core"));
        Assert.Equal("Users", typeDetector.ExtractModuleName("Users.Infrastructure", "infrastructure"));
        Assert.Equal("Users", typeDetector.ExtractModuleName("Users.Private.App", "private-app"));
        Assert.Equal("Shared", typeDetector.ExtractModuleName("Shared.Core", "shared-core"));
    }
}