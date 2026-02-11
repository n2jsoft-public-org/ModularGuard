using n2jSoft.ModularGuard.CLI.Models;

namespace n2jSoft.ModularGuard.CLI.Validation;

public sealed class ValidationResult
{
    public ValidationResult(IReadOnlyList<Violation> violations)
    {
        Violations = violations;
    }

    public IReadOnlyList<Violation> Violations { get; }

    public int ErrorCount => Violations.Count(v => v.Severity == ViolationSeverity.Error);
    public int WarningCount => Violations.Count(v => v.Severity == ViolationSeverity.Warning);
    public int InfoCount => Violations.Count(v => v.Severity == ViolationSeverity.Info);

    public bool HasErrors => ErrorCount > 0;
    public bool HasWarnings => WarningCount > 0;
    public bool IsValid => !HasErrors;
}