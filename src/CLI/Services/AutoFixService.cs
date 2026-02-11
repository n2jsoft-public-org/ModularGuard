using System.Xml.Linq;
using n2jSoft.ModularGuard.CLI.Models;

namespace n2jSoft.ModularGuard.CLI.Services;

/// <summary>
///     Service responsible for automatically fixing violations by removing invalid project references
/// </summary>
public sealed class AutoFixService
{
    /// <summary>
    ///     Removes an invalid project reference from a .csproj file
    /// </summary>
    /// <param name="projectFilePath">Path to the .csproj file</param>
    /// <param name="invalidReferenceName">Name of the invalid project reference to remove</param>
    /// <param name="dryRun">If true, only validates the fix without applying it</param>
    /// <returns>Result of the fix operation</returns>
    public AutoFixResult RemoveInvalidReference(string projectFilePath, string invalidReferenceName,
        bool dryRun = false)
    {
        try
        {
            if (!File.Exists(projectFilePath))
            {
                return new AutoFixResult(
                    false,
                    $"Project file not found: {projectFilePath}",
                    projectFilePath,
                    false);
            }

            // Load the project file as XML
            var doc = XDocument.Load(projectFilePath);
            var projectReferences = doc.Descendants("ProjectReference").ToList();

            // Find the reference to remove
            XElement? referenceToRemove = null;
            foreach (var reference in projectReferences)
            {
                var includeAttr = reference.Attribute("Include");
                if (includeAttr != null)
                {
                    var referencedProjectName = GetProjectNameFromPath(includeAttr.Value);
                    if (referencedProjectName.Equals(invalidReferenceName, StringComparison.OrdinalIgnoreCase))
                    {
                        referenceToRemove = reference;
                        break;
                    }
                }
            }

            if (referenceToRemove == null)
            {
                return new AutoFixResult(
                    false,
                    $"Reference to '{invalidReferenceName}' not found in project file",
                    projectFilePath,
                    false);
            }

            if (dryRun)
            {
                return new AutoFixResult(
                    true,
                    $"[DRY RUN] Would remove reference to '{invalidReferenceName}' from {Path.GetFileName(projectFilePath)}",
                    projectFilePath,
                    false);
            }

            // Remove the reference
            referenceToRemove.Remove();

            // Clean up empty ItemGroup elements
            var itemGroups = doc.Descendants("ItemGroup").ToList();
            foreach (var itemGroup in itemGroups)
            {
                if (!itemGroup.HasElements)
                {
                    itemGroup.Remove();
                }
            }

            // Save the modified project file
            doc.Save(projectFilePath);

            return new AutoFixResult(
                true,
                $"Successfully removed reference to '{invalidReferenceName}' from {Path.GetFileName(projectFilePath)}",
                projectFilePath,
                true);
        }
        catch (Exception ex)
        {
            return new AutoFixResult(
                false,
                $"Failed to fix project file: {ex.Message}",
                projectFilePath,
                false);
        }
    }

    /// <summary>
    ///     Attempts to automatically fix all violations in a validation result
    /// </summary>
    /// <param name="violations">List of violations to fix</param>
    /// <param name="allModules">All modules in the solution</param>
    /// <param name="dryRun">If true, only validates fixes without applying them</param>
    /// <returns>Collection of fix results</returns>
    public IReadOnlyList<AutoFixResult> FixViolations(
        IEnumerable<Violation> violations,
        IReadOnlyList<ModuleInfo> allModules,
        bool dryRun = false)
    {
        var results = new List<AutoFixResult>();

        foreach (var violation in violations)
        {
            // Only auto-fix violations that are reference-related
            if (!IsFixableViolation(violation))
            {
                results.Add(new AutoFixResult(
                    false,
                    $"Violation '{violation.RuleName}' cannot be automatically fixed",
                    GetProjectFilePath(violation.ProjectName, allModules) ?? string.Empty,
                    false));
                continue;
            }

            var projectFilePath = GetProjectFilePath(violation.ProjectName, allModules);
            if (string.IsNullOrEmpty(projectFilePath))
            {
                results.Add(new AutoFixResult(
                    false,
                    $"Could not find project file for '{violation.ProjectName}'",
                    string.Empty,
                    false));
                continue;
            }

            var result = RemoveInvalidReference(projectFilePath, violation.InvalidReference, dryRun);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    ///     Determines if a violation can be automatically fixed
    /// </summary>
    private static bool IsFixableViolation(Violation violation)
    {
        // Auto-fix is safe for violations that involve removing invalid references
        // We avoid auto-fixing warnings and info messages to prevent unintended changes
        return violation.Severity == ViolationSeverity.Error &&
               !string.IsNullOrWhiteSpace(violation.InvalidReference);
    }

    /// <summary>
    ///     Extracts the project name from a project reference path
    /// </summary>
    private static string GetProjectNameFromPath(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        return fileName;
    }

    /// <summary>
    ///     Gets the file path for a project by its name
    /// </summary>
    private static string? GetProjectFilePath(string projectName, IReadOnlyList<ModuleInfo> allModules)
    {
        var module = allModules.FirstOrDefault(m =>
            m.ProjectInfo.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

        return module?.ProjectInfo.FilePath;
    }
}

/// <summary>
///     Result of an auto-fix operation
/// </summary>
public sealed record AutoFixResult(
    bool Success,
    string Message,
    string FilePath,
    bool ChangesMade);