using n2jSoft.ModularGuard.CLI.Models;
using n2jSoft.ModularGuard.CLI.Validation.Rules;

namespace n2jSoft.ModularGuard.CLI.Tests.Validation.Rules;

public sealed class UnknownProjectTypeRuleTests
{
    [Fact]
    public void AppliesTo_UnknownProjectType_ReturnsTrue()
    {
        // Arrange
        var rule = new UnknownProjectTypeRule();
        var module = CreateModule("SomeProject", "unknown");

        // Act
        var result = rule.AppliesTo(module);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void AppliesTo_KnownProjectType_ReturnsFalse()
    {
        // Arrange
        var rule = new UnknownProjectTypeRule();
        var module = CreateModule("Module1.Core", "core");

        // Act
        var result = rule.AppliesTo(module);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Validate_UnknownProjectType_ReturnsViolation()
    {
        // Arrange
        var rule = new UnknownProjectTypeRule();
        var module = CreateModule("UnmatchedProject", "unknown");
        var allModules = new List<ModuleInfo> { module };

        // Act
        var violations = rule.Validate(module, allModules).ToList();

        // Assert
        Assert.Single(violations);
        var violation = violations[0];
        Assert.Equal("UnmatchedProject", violation.ProjectName);
        Assert.Equal("N/A", violation.InvalidReference);
        Assert.Equal("UnknownProjectTypeRule", violation.RuleName);
        Assert.Equal(ViolationSeverity.Error, violation.Severity);
        Assert.Contains("does not match any module or shared pattern", violation.Description);
        Assert.Contains("ignoredProjects", violation.Description);
    }

    [Fact]
    public void Validate_MultipleUnknownProjects_ReturnsViolationForEach()
    {
        // Arrange
        var rule = new UnknownProjectTypeRule();
        var module1 = CreateModule("UnmatchedProject1", "unknown");
        var module2 = CreateModule("UnmatchedProject2", "unknown");
        var allModules = new List<ModuleInfo> { module1, module2 };

        // Act
        var violations1 = rule.Validate(module1, allModules).ToList();
        var violations2 = rule.Validate(module2, allModules).ToList();

        // Assert
        Assert.Single(violations1);
        Assert.Single(violations2);
        Assert.Equal("UnmatchedProject1", violations1[0].ProjectName);
        Assert.Equal("UnmatchedProject2", violations2[0].ProjectName);
    }

    [Fact]
    public void RuleName_ReturnsCorrectName()
    {
        // Arrange
        var rule = new UnknownProjectTypeRule();

        // Act
        var ruleName = rule.RuleName;

        // Assert
        Assert.Equal("UnknownProjectTypeRule", ruleName);
    }

    private static ModuleInfo CreateModule(string projectName, string projectType)
    {
        var projectInfo = new ProjectInfo(
            projectName,
            $"/path/to/{projectName}.csproj",
            new List<ProjectReferenceInfo>());

        return new ModuleInfo(
            projectType == "unknown" ? "Unknown" : "Module1",
            projectType,
            projectInfo);
    }
}