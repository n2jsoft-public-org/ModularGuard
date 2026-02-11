using n2jSoft.ModularGuard.CLI.Configuration;

namespace n2jSoft.ModularGuard.CLI.Tests.Configuration;

public sealed class ConfigurableProjectTypeDetectorTests
{
    [Fact]
    public void DetectProjectType_WithDefaultConfig_DetectsCoreProject()
    {
        // Arrange
        var loader = new ConfigurationLoader();
        var config = loader.CreateDefaultConfiguration();
        var detector = new ConfigurableProjectTypeDetector(config);

        // Act
        var result = detector.DetectProjectType("Module1.Core");

        // Assert
        Assert.Equal("core", result);
    }

    [Fact]
    public void DetectProjectType_WithDefaultConfig_DetectsInfrastructureProject()
    {
        // Arrange
        var loader = new ConfigurationLoader();
        var config = loader.CreateDefaultConfiguration();
        var detector = new ConfigurableProjectTypeDetector(config);

        // Act
        var result = detector.DetectProjectType("Module1.Infrastructure");

        // Assert
        Assert.Equal("infrastructure", result);
    }

    [Fact]
    public void ExtractModuleName_WithDefaultConfig_ExtractsCorrectly()
    {
        // Arrange
        var loader = new ConfigurationLoader();
        var config = loader.CreateDefaultConfiguration();
        var detector = new ConfigurableProjectTypeDetector(config);

        // Act
        var result = detector.ExtractModuleName("Module1.Core", "core");

        // Assert
        Assert.Equal("Module1", result);
    }

    [Fact]
    public void ExtractModuleName_SharedProject_ReturnsShared()
    {
        // Arrange
        var loader = new ConfigurationLoader();
        var config = loader.CreateDefaultConfiguration();
        var detector = new ConfigurableProjectTypeDetector(config);

        // Act
        var result = detector.ExtractModuleName("Shared.Core", "shared-core");

        // Assert
        Assert.Equal("Shared", result);
    }

    [Fact]
    public void DetectProjectType_SharedCore_ReturnsCorrectType()
    {
        // Arrange
        var loader = new ConfigurationLoader();
        var config = loader.CreateDefaultConfiguration();
        var detector = new ConfigurableProjectTypeDetector(config);

        // Act
        var result = detector.DetectProjectType("Shared.Core");

        // Assert
        Assert.Equal("shared-core", result);
    }

    [Fact]
    public void DetectProjectType_ModuleSharedEvents_ReturnsCorrectType()
    {
        // Arrange
        var loader = new ConfigurationLoader();
        var config = loader.CreateDefaultConfiguration();
        var detector = new ConfigurableProjectTypeDetector(config);

        // Act
        var result = detector.DetectProjectType("Module1.Shared.Events");

        // Assert
        Assert.Equal("shared-events", result);
    }

    [Fact]
    public void DetectProjectType_WithWorkingDirectory_SharedProjectInSharedDir_ReturnsSharedType()
    {
        // Arrange
        var config = new LinterConfiguration
        {
            Modules = new ModuleConfiguration
            {
                Patterns = new List<ProjectPattern>
                {
                    new() { Name = "Core", Pattern = "*.Core", Type = "core", ModuleExtraction = "^(.+)\\.Core$" }
                }
            },
            Shared = new SharedConfiguration
            {
                WorkingDirectory = "src/Shared",
                Patterns = new List<ProjectPattern>
                {
                    new() { Name = "SharedCore", Pattern = "Shared.Core", Type = "shared-core" }
                }
            }
        };
        var detector = new ConfigurableProjectTypeDetector(config);
        var projectPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "Shared", "Shared.Core.csproj");
        var rootPath = Directory.GetCurrentDirectory();

        // Act
        var result = detector.DetectProjectType("Shared.Core", projectPath, rootPath);

        // Assert
        Assert.Equal("shared-core", result);
    }

    [Fact]
    public void DetectProjectType_WithWorkingDirectory_ModuleProjectOutsideSharedDir_ReturnsModuleType()
    {
        // Arrange
        var config = new LinterConfiguration
        {
            Modules = new ModuleConfiguration
            {
                Patterns = new List<ProjectPattern>
                {
                    new() { Name = "Core", Pattern = "*.Core", Type = "core", ModuleExtraction = "^(.+)\\.Core$" }
                }
            },
            Shared = new SharedConfiguration
            {
                WorkingDirectory = "src/Shared",
                Patterns = new List<ProjectPattern>
                {
                    new() { Name = "SharedCore", Pattern = "Shared.Core", Type = "shared-core" }
                }
            }
        };
        var detector = new ConfigurableProjectTypeDetector(config);
        var projectPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "Modules", "Module1.Core.csproj");
        var rootPath = Directory.GetCurrentDirectory();

        // Act
        var result = detector.DetectProjectType("Module1.Core", projectPath, rootPath);

        // Assert
        Assert.Equal("core", result);
    }

    [Fact]
    public void DetectProjectType_WithWorkingDirectory_SharedPatternOutsideSharedDir_MatchesModulePattern()
    {
        // Arrange
        var config = new LinterConfiguration
        {
            Modules = new ModuleConfiguration
            {
                Patterns = new List<ProjectPattern>
                {
                    new() { Name = "Core", Pattern = "*.Core", Type = "core", ModuleExtraction = "^(.+)\\.Core$" }
                }
            },
            Shared = new SharedConfiguration
            {
                WorkingDirectory = "src/Shared",
                Patterns = new List<ProjectPattern>
                {
                    new() { Name = "SharedCore", Pattern = "Shared.Core", Type = "shared-core" }
                }
            }
        };
        var detector = new ConfigurableProjectTypeDetector(config);
        // Project named "Shared.Core" but outside shared directory
        var projectPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "Modules", "Shared.Core.csproj");
        var rootPath = Directory.GetCurrentDirectory();

        // Act
        var result = detector.DetectProjectType("Shared.Core", projectPath, rootPath);

        // Assert - Should match module pattern *.Core because it's outside the shared directory
        Assert.Equal("core", result);
    }

    [Fact]
    public void DetectProjectType_WithoutWorkingDirectory_FallsBackToNameBasedMatching()
    {
        // Arrange
        var config = new LinterConfiguration
        {
            Modules = new ModuleConfiguration
            {
                Patterns = new List<ProjectPattern>
                {
                    new() { Name = "Core", Pattern = "*.Core", Type = "core", ModuleExtraction = "^(.+)\\.Core$" }
                }
            },
            Shared = new SharedConfiguration
            {
                WorkingDirectory = null, // No working directory
                Patterns = new List<ProjectPattern>
                {
                    new() { Name = "SharedCore", Pattern = "Shared.Core", Type = "shared-core" }
                }
            }
        };
        var detector = new ConfigurableProjectTypeDetector(config);
        var projectPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "Modules", "Shared.Core.csproj");
        var rootPath = Directory.GetCurrentDirectory();

        // Act
        var result = detector.DetectProjectType("Shared.Core", projectPath, rootPath);

        // Assert - Should use name-based matching (shared first)
        Assert.Equal("shared-core", result);
    }

    [Fact]
    public void DetectProjectType_WithWorkingDirectory_NormalizedPathHandling()
    {
        // Arrange
        var config = new LinterConfiguration
        {
            Modules = new ModuleConfiguration
            {
                Patterns = new List<ProjectPattern>
                {
                    new() { Name = "Core", Pattern = "*.Core", Type = "core", ModuleExtraction = "^(.+)\\.Core$" }
                }
            },
            Shared = new SharedConfiguration
            {
                WorkingDirectory = "src\\Shared\\", // Windows-style path with trailing slash
                Patterns = new List<ProjectPattern>
                {
                    new() { Name = "SharedCore", Pattern = "Shared.Core", Type = "shared-core" }
                }
            }
        };
        var detector = new ConfigurableProjectTypeDetector(config);
        var projectPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "Shared", "Shared.Core.csproj");
        var rootPath = Directory.GetCurrentDirectory();

        // Act
        var result = detector.DetectProjectType("Shared.Core", projectPath, rootPath);

        // Assert - Should handle path normalization correctly
        Assert.Equal("shared-core", result);
    }
}