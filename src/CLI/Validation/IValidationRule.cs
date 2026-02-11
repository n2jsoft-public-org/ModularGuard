using n2jSoft.ModularGuard.CLI.Models;

namespace n2jSoft.ModularGuard.CLI.Validation;

public interface IValidationRule
{
    string RuleName { get; }
    bool AppliesTo(ModuleInfo module);
    IEnumerable<Violation> Validate(ModuleInfo module, IReadOnlyList<ModuleInfo> allModules);
}