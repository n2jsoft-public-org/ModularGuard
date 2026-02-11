using n2jSoft.ModularGuard.CLI.Models;

namespace n2jSoft.ModularGuard.CLI.Validation.Rules;

public sealed class UnknownProjectTypeRule : IValidationRule
{
    public string RuleName => "UnknownProjectTypeRule";

    public bool AppliesTo(ModuleInfo module)
    {
        return module.Type == "unknown";
    }

    public IEnumerable<Violation> Validate(ModuleInfo module, IReadOnlyList<ModuleInfo> allModules)
    {
        yield return new Violation(
            module.ProjectInfo.Name,
            "N/A",
            RuleName,
            $"Project '{module.ProjectInfo.Name}' does not match any module or shared pattern. " +
            "Update configuration to add a matching pattern, or add this project to 'ignoredProjects' if it should be excluded from validation.",
            ViolationSeverity.Error,
            $"Add a project type pattern in the configuration file, or add '{module.ProjectInfo.Name}' to 'ignoredProjects' list.",
            "https://github.com/n2jsoft/modularguard/blob/main/docs/configuration.md");
    }
}