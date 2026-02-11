using n2jSoft.ModularGuard.CLI.Services;

namespace n2jSoft.ModularGuard.CLI.Tests.Services;

public sealed class ProjectTypeDetectorTests
{
    private readonly ProjectTypeDetector _detector = new();

    [Theory]
    [InlineData("Module1.Core", "core")]
    [InlineData("Module1.Infrastructure", "infrastructure")]
    [InlineData("Module1.Admin.App", "admin-app")]
    [InlineData("Module1.Admin.Endpoints", "admin-endpoints")]
    [InlineData("Module1.Private.App", "private-app")]
    [InlineData("Module1.Private.Endpoints", "private-endpoints")]
    [InlineData("Module1.Public.App", "public-app")]
    [InlineData("Module1.Public.Endpoints", "public-endpoints")]
    [InlineData("Module1.Shared.Events", "shared-events")]
    [InlineData("Module1.Shared.Messages", "shared-messages")]
    [InlineData("Shared.Core", "shared-core")]
    [InlineData("Shared.Infrastructure", "shared-infrastructure")]
    [InlineData("Shared.App.Admin", "shared-app-admin")]
    [InlineData("Shared.App.Private", "shared-app-private")]
    [InlineData("Shared.App.Public", "shared-app-public")]
    [InlineData("RandomProject", "unknown")]
    public void DetectProjectType_ShouldReturnCorrectType(string projectName, string expectedType)
    {
        // Act
        var result = _detector.DetectProjectType(projectName);

        // Assert
        Assert.Equal(expectedType, result);
    }

    [Theory]
    [InlineData("Module1.Core", "core", "Module1")]
    [InlineData("Module1.Infrastructure", "infrastructure", "Module1")]
    [InlineData("Module1.Admin.App", "admin-app", "Module1")]
    [InlineData("Module1.Admin.Endpoints", "admin-endpoints", "Module1")]
    [InlineData("UserManagement.Core", "core", "UserManagement")]
    [InlineData("Shared.Core", "shared-core", "Shared")]
    [InlineData("Shared.App.Admin", "shared-app-admin", "Shared")]
    public void ExtractModuleName_ShouldReturnCorrectModuleName(string projectName, string projectType,
        string expectedModuleName)
    {
        // Act
        var result = _detector.ExtractModuleName(projectName, projectType);

        // Assert
        Assert.Equal(expectedModuleName, result);
    }
}