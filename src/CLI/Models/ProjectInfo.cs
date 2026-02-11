namespace n2jSoft.ModularGuard.CLI.Models;

public sealed record ProjectInfo(
    string Name,
    string FilePath,
    IReadOnlyList<ProjectReferenceInfo> ProjectReferences);

public sealed record ProjectReferenceInfo(
    string Path,
    string? OutputItemType,
    bool ReferenceOutputAssembly,
    string? FilePath = null,
    int? LineNumber = null,
    int? ColumnNumber = null)
{
    /// <summary>
    ///     Returns true if this is a special reference that should not be considered for optimization
    ///     (e.g., Analyzers, source generators, or build-time only references)
    /// </summary>
    public bool IsSpecialReference()
    {
        // Analyzer references (source generators, code analyzers)
        if (!string.IsNullOrEmpty(OutputItemType) &&
            OutputItemType.Equals("Analyzer", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Build-time only references that don't produce runtime assemblies
        if (!ReferenceOutputAssembly)
        {
            return true;
        }

        return false;
    }
}