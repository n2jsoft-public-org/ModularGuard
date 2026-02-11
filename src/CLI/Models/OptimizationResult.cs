namespace n2jSoft.ModularGuard.CLI.Models;

public sealed record OptimizationResult(
    string ProjectName,
    string ProjectPath,
    IReadOnlyList<UnnecessaryReference> UnnecessaryReferences);

public sealed record UnnecessaryReference(
    string ReferenceName,
    UnnecessaryReferenceReason Reason,
    string? TransitivePath = null);

public enum UnnecessaryReferenceReason
{
    Unused,
    Transitive
}