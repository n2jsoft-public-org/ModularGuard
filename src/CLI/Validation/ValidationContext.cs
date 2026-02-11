using n2jSoft.ModularGuard.CLI.Models;

namespace n2jSoft.ModularGuard.CLI.Validation;

public sealed class ValidationContext
{
    public ValidationContext(IReadOnlyList<ModuleInfo> allModules)
    {
        AllModules = allModules;
    }

    public IReadOnlyList<ModuleInfo> AllModules { get; }

    public string? GetProjectNameFromReference(ProjectReferenceInfo projectReference)
    {
        // ProjectReference can be a relative path like "../Module.Core/Module.Core.csproj"
        var fileName = Path.GetFileNameWithoutExtension(projectReference.Path);
        return fileName;
    }

    public ModuleInfo? FindModuleByProjectName(string projectName)
    {
        return AllModules.FirstOrDefault(m =>
            m.ProjectInfo.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));
    }
}