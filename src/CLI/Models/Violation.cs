namespace n2jSoft.ModularGuard.CLI.Models;

public sealed record Violation(
    string ProjectName,
    string InvalidReference,
    string RuleName,
    string Description,
    ViolationSeverity Severity,
    string? Suggestion = null,
    string? DocumentationUrl = null,
    bool IsAutoFixable = true);

public enum ViolationSeverity
{
    Error,
    Warning,
    Info
}