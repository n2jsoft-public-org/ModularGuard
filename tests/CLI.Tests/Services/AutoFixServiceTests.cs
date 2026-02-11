using System.Text.RegularExpressions;
using n2jSoft.ModularGuard.CLI.Models;
using n2jSoft.ModularGuard.CLI.Services;

namespace n2jSoft.ModularGuard.CLI.Tests.Services;

public sealed class AutoFixServiceTests
{
    private readonly AutoFixService _autoFixService;

    public AutoFixServiceTests()
    {
        _autoFixService = new AutoFixService();
    }

    [Fact]
    public void RemoveInvalidReference_WithNonExistentFile_ReturnsFail()
    {
        // Arrange
        var nonExistentPath = "/tmp/nonexistent.csproj";

        // Act
        var result = _autoFixService.RemoveInvalidReference(nonExistentPath, "SomeProject");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Message);
    }

    [Fact]
    public void RemoveInvalidReference_WithDryRun_DoesNotModifyFile()
    {
        // Arrange
        var tempFile = CreateTempProjectFile();
        var originalContent = File.ReadAllText(tempFile);

        // Act
        var result = _autoFixService.RemoveInvalidReference(tempFile, "Users.Core", true);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("DRY RUN", result.Message);
        Assert.False(result.ChangesMade);
        Assert.Equal(originalContent, File.ReadAllText(tempFile));

        // Cleanup
        File.Delete(tempFile);
    }

    [Fact]
    public void RemoveInvalidReference_WithValidReference_RemovesReference()
    {
        // Arrange
        var tempFile = CreateTempProjectFile();

        // Act
        var result = _autoFixService.RemoveInvalidReference(tempFile, "Users.Core", false);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.ChangesMade);

        var modifiedContent = File.ReadAllText(tempFile);
        Assert.DoesNotContain("Users.Core", modifiedContent);
        Assert.Contains("Shared.Core", modifiedContent); // Other references should remain

        // Cleanup
        File.Delete(tempFile);
    }

    [Fact]
    public void RemoveInvalidReference_WithNonExistentReference_ReturnsFail()
    {
        // Arrange
        var tempFile = CreateTempProjectFile();

        // Act
        var result = _autoFixService.RemoveInvalidReference(tempFile, "NonExistent.Project", false);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Message);

        // Cleanup
        File.Delete(tempFile);
    }

    [Fact]
    public void FixViolations_WithMultipleViolations_FixesAll()
    {
        // Arrange
        var tempFile = CreateTempProjectFile();
        var projectInfo = new ProjectInfo(
            "Orders.Infrastructure",
            tempFile,
            new List<ProjectReferenceInfo>
            {
                new("../Users/Users.Core/Users.Core.csproj", null, true),
                new("../Shared/Shared.Core/Shared.Core.csproj", null, true)
            });

        var modules = new List<ModuleInfo>
        {
            new("Orders", "Infrastructure", projectInfo)
        };

        var violations = new List<Violation>
        {
            new(
                "Orders.Infrastructure",
                "Users.Core",
                "TestRule",
                "Invalid reference",
                ViolationSeverity.Error,
                IsAutoFixable: true)
        };

        // Act
        var results = _autoFixService.FixViolations(violations, modules, false);

        // Assert
        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.True(results[0].ChangesMade);

        // Cleanup
        File.Delete(tempFile);
    }

    [Fact]
    public void FixViolations_WithUnfixableViolations_SkipsThem()
    {
        // Arrange
        var tempFile = CreateTempProjectFile();
        var projectInfo = new ProjectInfo(
            "Orders.Infrastructure",
            tempFile,
            new List<ProjectReferenceInfo>());

        var modules = new List<ModuleInfo>
        {
            new("Orders", "Infrastructure", projectInfo)
        };

        var violations = new List<Violation>
        {
            new(
                "Orders.Infrastructure",
                "",
                "TestRule",
                "Invalid reference",
                ViolationSeverity.Warning,
                IsAutoFixable: false)
        };

        // Act
        var results = _autoFixService.FixViolations(violations, modules, false);

        // Assert
        Assert.Single(results);
        Assert.False(results[0].Success);
        Assert.Contains("cannot be automatically fixed", results[0].Message);

        // Cleanup
        File.Delete(tempFile);
    }

    [Fact]
    public void RemoveInvalidReference_RemovesEmptyItemGroups()
    {
        // Arrange
        var tempFile = CreateTempProjectFileWithSingleReference();

        // Act
        var result = _autoFixService.RemoveInvalidReference(tempFile, "Users.Core", false);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.ChangesMade);

        var modifiedContent = File.ReadAllText(tempFile);
        // The ItemGroup should be removed entirely since it's now empty
        var itemGroupCount = Regex.Matches(modifiedContent, "<ItemGroup").Count;
        Assert.Equal(0, itemGroupCount);

        // Cleanup
        File.Delete(tempFile);
    }

    private static string CreateTempProjectFile()
    {
        var tempFile = Path.GetTempFileName();
        File.Move(tempFile, tempFile + ".csproj");
        tempFile = tempFile + ".csproj";

        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include=""../Users/Users.Core/Users.Core.csproj"" />
    <ProjectReference Include=""../Shared/Shared.Core/Shared.Core.csproj"" />
  </ItemGroup>
</Project>";

        File.WriteAllText(tempFile, content);
        return tempFile;
    }

    private static string CreateTempProjectFileWithSingleReference()
    {
        var tempFile = Path.GetTempFileName();
        File.Move(tempFile, tempFile + ".csproj");
        tempFile = tempFile + ".csproj";

        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include=""../Users/Users.Core/Users.Core.csproj"" />
  </ItemGroup>
</Project>";

        File.WriteAllText(tempFile, content);
        return tempFile;
    }
}