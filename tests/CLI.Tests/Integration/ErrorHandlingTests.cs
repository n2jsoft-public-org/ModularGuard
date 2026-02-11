using n2jSoft.ModularGuard.CLI.Configuration;
using n2jSoft.ModularGuard.CLI.Services;

namespace n2jSoft.ModularGuard.CLI.Tests.Integration;

[Collection("Assembly")]
public class ErrorHandlingTests
{
    [Fact]
    public void LoadProject_WithInvalidPath_ShouldThrowException()
    {
        // Arrange
        var invalidPath = "/non/existent/path/to/project.csproj";

        // Act & Assert
        using var loaderService = new ProjectLoaderService();
        Assert.Throws<InvalidOperationException>(() => loaderService.LoadProject(invalidPath));
    }

    [Fact]
    public void DiscoverProjects_WithNullPath_ShouldReturnEmpty()
    {
        // Arrange
        var discoveryService = new ProjectDiscoveryService();

        // Act
        var projects = discoveryService.DiscoverProjects(null!).ToList();

        // Assert
        Assert.Empty(projects);
    }

    [Fact]
    public void DiscoverProjects_WithEmptyPath_ShouldReturnEmpty()
    {
        // Arrange
        var discoveryService = new ProjectDiscoveryService();

        // Act
        var projects = discoveryService.DiscoverProjects(string.Empty).ToList();

        // Assert
        Assert.Empty(projects);
    }

    [Fact]
    public void ConfigurationLoader_WithInvalidJson_ShouldReturnNull()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var configPath = Path.Combine(tempDir, ".mmlinter.json");
        File.WriteAllText(configPath, "{ invalid json }");

        var loader = new ConfigurationLoader();

        try
        {
            // Act
            var config = loader.TryLoadConfiguration(tempDir);

            // Assert
            Assert.Null(config);
        }
        finally
        {
            // Cleanup
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ConfigurationLoader_WithMissingFile_ShouldReturnNull()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var loader = new ConfigurationLoader();

        try
        {
            // Act
            var config = loader.TryLoadConfiguration(tempDir);

            // Assert
            Assert.Null(config);
        }
        finally
        {
            // Cleanup
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ProjectTypeDetector_WithUnknownProjectName_ShouldReturnUnknown()
    {
        // Arrange
        var configuration = new LinterConfiguration();
        var detector = new ConfigurableProjectTypeDetector(configuration);

        // Act
        var projectType = detector.DetectProjectType("SomeRandomProject");

        // Assert
        Assert.Equal("unknown", projectType);
    }

    [Fact]
    public void ProjectLoaderService_DisposedInstance_ShouldThrowException()
    {
        // Arrange
        var loaderService = new ProjectLoaderService();
        loaderService.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() =>
            loaderService.LoadProject("any/path.csproj"));
    }

    [Fact]
    public void ProjectLoaderService_MultipleDisposes_ShouldNotThrow()
    {
        // Arrange
        var loaderService = new ProjectLoaderService();

        // Act & Assert - Should not throw
        loaderService.Dispose();
        loaderService.Dispose();
        loaderService.Dispose();
    }

    [Fact]
    public void ConfigurationLoader_CreateDefaultConfiguration_ShouldNeverReturnNull()
    {
        // Arrange
        var loader = new ConfigurationLoader();

        // Act
        var config = loader.CreateDefaultConfiguration();

        // Assert
        Assert.NotNull(config);
        Assert.NotEmpty(config.Modules.Patterns);
        Assert.NotEmpty(config.Shared.Patterns);
        Assert.NotEmpty(config.DependencyRules);
    }

    [Fact]
    public void DiscoverProjects_WithAccessDeniedDirectory_ShouldHandleGracefully()
    {
        // This test is platform-specific and might need adjustment
        // For now, we'll test with a non-existent directory

        // Arrange
        var discoveryService = new ProjectDiscoveryService();
        var inaccessiblePath = "/root/restricted"; // Unix-style restricted path

        // Act
        var projects = discoveryService.DiscoverProjects(inaccessiblePath).ToList();

        // Assert
        Assert.Empty(projects); // Should return empty, not throw
    }

    [Fact]
    public void ProjectLoader_WithCircularReferences_ShouldLoadBoth()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var project1Path = Path.Combine(tempDir, "Project1.csproj");
        var project2Path = Path.Combine(tempDir, "Project2.csproj");

        // Create projects with circular references
        File.WriteAllText(project1Path, @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""Project2.csproj"" />
  </ItemGroup>
</Project>");

        File.WriteAllText(project2Path, @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""Project1.csproj"" />
  </ItemGroup>
</Project>");

        try
        {
            // Act - Should not throw, even with circular references
            using var loaderService = new ProjectLoaderService();
            var project1 = loaderService.LoadProject(project1Path);
            var project2 = loaderService.LoadProject(project2Path);

            // Assert
            Assert.NotNull(project1);
            Assert.NotNull(project2);
            Assert.Single(project1.ProjectReferences);
            Assert.Single(project2.ProjectReferences);
        }
        finally
        {
            // Cleanup
            Directory.Delete(tempDir, true);
        }
    }
}