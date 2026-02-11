using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using n2jSoft.ModularGuard.CLI.Models;

namespace n2jSoft.ModularGuard.CLI.Services;

public sealed class ProjectReferenceAnalyzer
{
    private readonly ProjectLoaderService _projectLoader;

    public ProjectReferenceAnalyzer(ProjectLoaderService projectLoader)
    {
        _projectLoader = projectLoader;
    }

    public IReadOnlyList<OptimizationResult> AnalyzeProjects(IReadOnlyList<ProjectInfo> projects)
    {
        var results = new List<OptimizationResult>();
        var projectMap = projects.ToDictionary(p => p.Name, p => p);

        foreach (var project in projects)
        {
            var unnecessaryReferences = new List<UnnecessaryReference>();

            // Detect transitive references
            var transitiveReferences = DetectTransitiveReferences(project, projectMap);
            unnecessaryReferences.AddRange(transitiveReferences);

            // Detect unused references (excluding those already marked as transitive)
            var transitiveReferenceNames = transitiveReferences.Select(r => r.ReferenceName).ToHashSet();
            var unusedReferences = DetectUnusedReferences(project, projectMap, transitiveReferenceNames);
            unnecessaryReferences.AddRange(unusedReferences);

            if (unnecessaryReferences.Count > 0)
            {
                results.Add(new OptimizationResult(
                    project.Name,
                    project.FilePath,
                    unnecessaryReferences));
            }
        }

        return results;
    }

    private IReadOnlyList<UnnecessaryReference> DetectTransitiveReferences(
        ProjectInfo project,
        Dictionary<string, ProjectInfo> projectMap)
    {
        var transitiveReferences = new List<UnnecessaryReference>();

        // Filter out special references (analyzers, build-time only, etc.)
        var normalReferences = project.ProjectReferences
            .Where(r => !r.IsSpecialReference())
            .ToList();

        var directReferences = GetResolvedProjectNames(normalReferences, projectMap);

        foreach (var directRef in directReferences)
        {
            if (!projectMap.TryGetValue(directRef, out var referencedProject))
            {
                continue;
            }

            // Get all transitive references from this direct reference
            var transitiveFromThis = GetAllTransitiveReferences(referencedProject, projectMap);

            // Check if any other direct reference is also accessible transitively
            foreach (var otherDirectRef in directReferences)
            {
                if (otherDirectRef == directRef)
                {
                    continue;
                }

                if (transitiveFromThis.Contains(otherDirectRef))
                {
                    // otherDirectRef is redundant because it's accessible through directRef
                    var transitivePath = $"{project.Name} → {directRef} → {otherDirectRef}";
                    transitiveReferences.Add(new UnnecessaryReference(
                        otherDirectRef,
                        UnnecessaryReferenceReason.Transitive,
                        transitivePath));
                }
            }
        }

        return transitiveReferences.DistinctBy(r => r.ReferenceName).ToList();
    }

    private HashSet<string> GetAllTransitiveReferences(
        ProjectInfo project,
        Dictionary<string, ProjectInfo> projectMap)
    {
        var result = new HashSet<string>();
        var visited = new HashSet<string>();
        var queue = new Queue<string>();

        // Start with direct references (excluding special references)
        var normalReferences = project.ProjectReferences
            .Where(r => !r.IsSpecialReference())
            .ToList();

        foreach (var directRef in GetResolvedProjectNames(normalReferences, projectMap))
        {
            queue.Enqueue(directRef);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (visited.Contains(current))
            {
                continue;
            }

            visited.Add(current);
            result.Add(current);

            if (projectMap.TryGetValue(current, out var currentProject))
            {
                var normalChildReferences = currentProject.ProjectReferences
                    .Where(r => !r.IsSpecialReference())
                    .ToList();

                foreach (var childRef in GetResolvedProjectNames(normalChildReferences, projectMap))
                {
                    if (!visited.Contains(childRef))
                    {
                        queue.Enqueue(childRef);
                    }
                }
            }
        }

        return result;
    }

    private IReadOnlyList<UnnecessaryReference> DetectUnusedReferences(
        ProjectInfo project,
        Dictionary<string, ProjectInfo> projectMap,
        HashSet<string> excludeReferences)
    {
        var unusedReferences = new List<UnnecessaryReference>();
        var projectDirectory = Path.GetDirectoryName(project.FilePath);

        if (string.IsNullOrEmpty(projectDirectory))
        {
            return unusedReferences;
        }

        // Get all C# files in the project directory
        var csFiles = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\"))
            .ToList();

        if (csFiles.Count == 0)
        {
            return unusedReferences;
        }

        // Parse all files and collect type references
        var usedNamespaces = new HashSet<string>();
        foreach (var csFile in csFiles)
        {
            try
            {
                var code = File.ReadAllText(csFile);
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetRoot();

                // Collect using directives
                var usingDirectives = root.DescendantNodes()
                    .OfType<UsingDirectiveSyntax>()
                    .Select(u => u.Name?.ToString())
                    .Where(n => n != null)
                    .Cast<string>();

                foreach (var ns in usingDirectives)
                {
                    usedNamespaces.Add(ns);
                }

                // Collect qualified type references (e.g., SomeNamespace.SomeType)
                var qualifiedNames = root.DescendantNodes()
                    .OfType<QualifiedNameSyntax>()
                    .Select(q => ExtractNamespace(q.ToString()))
                    .Where(n => n != null)
                    .Cast<string>();

                foreach (var ns in qualifiedNames)
                {
                    usedNamespaces.Add(ns);
                }
            }
            catch
            {
                // Skip files that can't be parsed
            }
        }

        // Check each reference to see if it's used (excluding special references)
        var normalReferences = project.ProjectReferences
            .Where(r => !r.IsSpecialReference())
            .ToList();

        var directReferences = GetResolvedProjectNames(normalReferences, projectMap);

        foreach (var referenceName in directReferences)
        {
            if (excludeReferences.Contains(referenceName))
            {
                continue;
            }

            // Check if any namespace from the code matches the reference project name
            var isUsed = usedNamespaces.Any(ns =>
                ns.Equals(referenceName, StringComparison.OrdinalIgnoreCase) ||
                ns.StartsWith(referenceName + ".", StringComparison.OrdinalIgnoreCase));

            if (!isUsed)
            {
                unusedReferences.Add(new UnnecessaryReference(
                    referenceName,
                    UnnecessaryReferenceReason.Unused));
            }
        }

        return unusedReferences;
    }

    private string? ExtractNamespace(string qualifiedName)
    {
        var parts = qualifiedName.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        // Return the first part as a potential namespace
        return parts[0];
    }

    private IReadOnlyList<string> GetResolvedProjectNames(
        IReadOnlyList<ProjectReferenceInfo> projectReferences,
        Dictionary<string, ProjectInfo> projectMap)
    {
        var resolvedNames = new List<string>();

        foreach (var reference in projectReferences)
        {
            // Extract project name from reference path
            var projectName = Path.GetFileNameWithoutExtension(reference.Path);

            // Try to find the actual project in our map
            if (projectMap.ContainsKey(projectName))
            {
                resolvedNames.Add(projectName);
            }
        }

        return resolvedNames;
    }
}