using n2jSoft.ModularGuard.CLI.Models;
using n2jSoft.ModularGuard.CLI.Validation;

namespace n2jSoft.ModularGuard.CLI.Tests.Validation;

public sealed class ValidationContextTests
{
    [Fact]
    public void GetProjectNameFromReference_ExtractsCorrectName()
    {
        // Arrange
        var context = new ValidationContext(Array.Empty<ModuleInfo>());
        var reference = new ProjectReferenceInfo("../Module1.Core/Module1.Core.csproj", null, true);

        // Act
        var result = context.GetProjectNameFromReference(reference);

        // Assert
        Assert.Equal("Module1.Core", result);
    }

    [Fact]
    public void FindModuleByProjectName_FindsExistingModule()
    {
        // Arrange
        var module1 = CreateModule("Module1.Core", "core");
        var module2 = CreateModule("Module1.Infrastructure", "infrastructure");
        var context = new ValidationContext(new[] { module1, module2 });

        // Act
        var result = context.FindModuleByProjectName("Module1.Core");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Module1.Core", result.ProjectInfo.Name);
    }

    [Fact]
    public void FindModuleByProjectName_ReturnsNullForNonExistent()
    {
        // Arrange
        var module1 = CreateModule("Module1.Core", "core");
        var context = new ValidationContext(new[] { module1 });

        // Act
        var result = context.FindModuleByProjectName("NonExistent");

        // Assert
        Assert.Null(result);
    }

    private static ModuleInfo CreateModule(string name, string type)
    {
        var projectInfo = new ProjectInfo(name, $"/path/to/{name}.csproj", Array.Empty<ProjectReferenceInfo>());
        var moduleName = name.Split('.')[0];
        return new ModuleInfo(moduleName, type, projectInfo);
    }
}