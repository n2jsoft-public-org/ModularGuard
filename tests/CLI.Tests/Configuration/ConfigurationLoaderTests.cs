using n2jSoft.ModularGuard.CLI.Configuration;

namespace n2jSoft.ModularGuard.CLI.Tests.Configuration;

public sealed class ConfigurationLoaderTests
{
    [Fact]
    public void CreateDefaultConfiguration_CreatesValidConfiguration()
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
        Assert.Contains(config.DependencyRules, kvp => kvp.Key == "core");
    }

    [Fact]
    public void CreateDefaultConfiguration_HasCoreRules()
    {
        // Arrange
        var loader = new ConfigurationLoader();

        // Act
        var config = loader.CreateDefaultConfiguration();

        // Assert
        Assert.True(config.DependencyRules.ContainsKey("core"));
        var coreRules = config.DependencyRules["core"];
        Assert.Contains("Shared.Core", coreRules.Allowed);
        Assert.Contains("*.Infrastructure", coreRules.Denied);
    }

    [Fact]
    public void TryLoadConfiguration_NoConfigFile_ReturnsNull()
    {
        // Arrange
        var loader = new ConfigurationLoader();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            var config = loader.TryLoadConfiguration(tempDir);

            // Assert
            Assert.Null(config);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}