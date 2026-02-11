using n2jSoft.ModularGuard.CLI.Configuration;
using n2jSoft.ModularGuard.CLI.Models;
using n2jSoft.ModularGuard.CLI.Services;
using n2jSoft.ModularGuard.CLI.Validation;

namespace n2jSoft.ModularGuard.CLI.Tests.Integration;

[Collection("Assembly")]
public class InvalidStructureIntegrationTests
{
    private readonly string _fixturePath;

    public InvalidStructureIntegrationTests()
    {
        _fixturePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Fixtures",
            "InvalidStructure");
    }

    [Fact]
    public void InvalidStructure_ShouldDetectViolations()
    {
        // Arrange
        var configLoader = new ConfigurationLoader();
        var configuration = configLoader.TryLoadConfiguration(_fixturePath)
                            ?? configLoader.CreateDefaultConfiguration();

        var discoveryService = new ProjectDiscoveryService();
        var projectPaths = discoveryService.DiscoverProjects(_fixturePath).ToList();

        var modules = LoadModules(projectPaths, configuration);

        // Act
        var validationEngine = new ConfigurableValidationEngine(configuration);
        var result = validationEngine.ValidateAllWithResult(modules);

        // Assert
        Assert.False(result.IsValid, "Invalid structure should have violations");
        Assert.NotEmpty(result.Violations);
        Assert.True(result.ErrorCount > 0, "Should have at least one error");
    }

    [Fact]
    public void InvalidStructure_CoreReferencingInfrastructure_ShouldBeViolation()
    {
        // Arrange
        var configLoader = new ConfigurationLoader();
        var configuration = configLoader.TryLoadConfiguration(_fixturePath)
                            ?? configLoader.CreateDefaultConfiguration();

        var discoveryService = new ProjectDiscoveryService();
        var projectPaths = discoveryService.DiscoverProjects(_fixturePath).ToList();

        var modules = LoadModules(projectPaths, configuration);

        // Act
        var validationEngine = new ConfigurableValidationEngine(configuration);
        var result = validationEngine.ValidateAllWithResult(modules);

        // Assert
        var coreViolation = result.Violations.FirstOrDefault(v =>
            v.ProjectName == "Orders.Core" &&
            v.InvalidReference == "Orders.Infrastructure");

        Assert.NotNull(coreViolation);
        Assert.Equal(ViolationSeverity.Error, coreViolation.Severity);
        Assert.NotNull(coreViolation.Suggestion);
    }

    [Fact]
    public void InvalidStructure_InfrastructureReferencingApp_ShouldBeViolation()
    {
        // Arrange
        var configLoader = new ConfigurationLoader();
        var configuration = configLoader.TryLoadConfiguration(_fixturePath)
                            ?? configLoader.CreateDefaultConfiguration();

        var discoveryService = new ProjectDiscoveryService();
        var projectPaths = discoveryService.DiscoverProjects(_fixturePath).ToList();

        var modules = LoadModules(projectPaths, configuration);

        // Act
        var validationEngine = new ConfigurableValidationEngine(configuration);
        var result = validationEngine.ValidateAllWithResult(modules);

        // Assert
        var infraViolation = result.Violations.FirstOrDefault(v =>
            v.ProjectName == "Orders.Infrastructure" &&
            v.InvalidReference == "Orders.Public.App");

        Assert.NotNull(infraViolation);
        Assert.Equal(ViolationSeverity.Error, infraViolation.Severity);
    }

    [Fact]
    public void InvalidStructure_AppReferencingEndpoints_ShouldBeViolation()
    {
        // Arrange
        var configLoader = new ConfigurationLoader();
        var configuration = configLoader.TryLoadConfiguration(_fixturePath)
                            ?? configLoader.CreateDefaultConfiguration();

        var discoveryService = new ProjectDiscoveryService();
        var projectPaths = discoveryService.DiscoverProjects(_fixturePath).ToList();

        var modules = LoadModules(projectPaths, configuration);

        // Act
        var validationEngine = new ConfigurableValidationEngine(configuration);
        var result = validationEngine.ValidateAllWithResult(modules);

        // Assert
        var appViolation = result.Violations.FirstOrDefault(v =>
            v.ProjectName == "Orders.Public.App" &&
            v.InvalidReference == "Orders.Public.Endpoints");

        Assert.NotNull(appViolation);
        Assert.Equal(ViolationSeverity.Error, appViolation.Severity);
    }

    [Fact]
    public void InvalidStructure_EndpointsReferencingCore_ShouldBeViolation()
    {
        // Arrange
        var configLoader = new ConfigurationLoader();
        var configuration = configLoader.TryLoadConfiguration(_fixturePath)
                            ?? configLoader.CreateDefaultConfiguration();

        var discoveryService = new ProjectDiscoveryService();
        var projectPaths = discoveryService.DiscoverProjects(_fixturePath).ToList();

        var modules = LoadModules(projectPaths, configuration);

        // Act
        var validationEngine = new ConfigurableValidationEngine(configuration);
        var result = validationEngine.ValidateAllWithResult(modules);

        // Assert
        var endpointsViolation = result.Violations.FirstOrDefault(v =>
            v.ProjectName == "Orders.Public.Endpoints" &&
            v.InvalidReference == "Orders.Core");

        Assert.NotNull(endpointsViolation);
        Assert.Equal(ViolationSeverity.Error, endpointsViolation.Severity);
    }

    [Fact]
    public void InvalidStructure_AllViolationsShouldHaveSuggestions()
    {
        // Arrange
        var configLoader = new ConfigurationLoader();
        var configuration = configLoader.TryLoadConfiguration(_fixturePath)
                            ?? configLoader.CreateDefaultConfiguration();

        var discoveryService = new ProjectDiscoveryService();
        var projectPaths = discoveryService.DiscoverProjects(_fixturePath).ToList();

        var modules = LoadModules(projectPaths, configuration);

        // Act
        var validationEngine = new ConfigurableValidationEngine(configuration);
        var result = validationEngine.ValidateAllWithResult(modules);

        // Assert
        Assert.All(result.Violations, v =>
        {
            Assert.NotNull(v.Suggestion);
            Assert.NotEmpty(v.Suggestion);
        });
    }

    [Fact]
    public void InvalidStructure_AllViolationsShouldHaveDocumentationLinks()
    {
        // Arrange
        var configLoader = new ConfigurationLoader();
        var configuration = configLoader.TryLoadConfiguration(_fixturePath)
                            ?? configLoader.CreateDefaultConfiguration();

        var discoveryService = new ProjectDiscoveryService();
        var projectPaths = discoveryService.DiscoverProjects(_fixturePath).ToList();

        var modules = LoadModules(projectPaths, configuration);

        // Act
        var validationEngine = new ConfigurableValidationEngine(configuration);
        var result = validationEngine.ValidateAllWithResult(modules);

        // Assert
        Assert.All(result.Violations, v =>
        {
            Assert.NotNull(v.DocumentationUrl);
            Assert.NotEmpty(v.DocumentationUrl);
        });
    }

    private List<ModuleInfo> LoadModules(List<string> projectPaths, LinterConfiguration configuration)
    {
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

        return modules;
    }
}