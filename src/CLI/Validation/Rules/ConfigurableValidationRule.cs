using System.Text.RegularExpressions;
using n2jSoft.ModularGuard.CLI.Configuration;
using n2jSoft.ModularGuard.CLI.Models;

namespace n2jSoft.ModularGuard.CLI.Validation.Rules;

/// <summary>
///     A validation rule that uses configuration to determine allowed/denied dependencies
/// </summary>
public sealed class ConfigurableValidationRule : IValidationRule
{
    private readonly LinterConfiguration _configuration;
    private readonly string _projectType;
    private readonly DependencyRuleConfiguration _ruleConfig;

    public ConfigurableValidationRule(
        string projectType,
        DependencyRuleConfiguration ruleConfig,
        LinterConfiguration configuration)
    {
        _projectType = projectType;
        _ruleConfig = ruleConfig;
        _configuration = configuration;
    }

    public string RuleName => $"ConfigurableRule({_projectType})";

    public bool AppliesTo(ModuleInfo module)
    {
        return module.Type.ToLowerInvariant() == _projectType.ToLowerInvariant() ||
               GetProjectTypeString(module) == _projectType;
    }

    public IEnumerable<Violation> Validate(ModuleInfo module, IReadOnlyList<ModuleInfo> allModules)
    {
        var context = new ValidationContext(allModules);

        foreach (var reference in module.ProjectInfo.ProjectReferences)
        {
            var referencedProjectName = context.GetProjectNameFromReference(reference);
            if (referencedProjectName == null)
            {
                continue;
            }

            var referencedModule = context.FindModuleByProjectName(referencedProjectName);
            if (referencedModule == null)
            {
                continue;
            }

            // Check if this reference is explicitly denied
            if (IsExplicitlyDenied(module, referencedModule))
            {
                var suggestion = GenerateSuggestion(module, referencedModule);
                yield return new Violation(
                    module.ProjectInfo.Name,
                    referencedProjectName,
                    RuleName,
                    $"Project of type '{_projectType}' cannot reference '{referencedProjectName}'. This reference is explicitly denied by configuration.",
                    ViolationSeverity.Error,
                    suggestion,
                    "https://github.com/n2jsoft/modularguard/blob/main/docs/rules.md",
                    IsAutoFixable: true,
                    FilePath: reference.FilePath,
                    LineNumber: reference.LineNumber,
                    ColumnNumber: reference.ColumnNumber);
                continue;
            }

            // Check if this reference is explicitly allowed
            if (!IsExplicitlyAllowed(module, referencedModule))
            {
                var suggestion = GenerateSuggestion(module, referencedModule);
                yield return new Violation(
                    module.ProjectInfo.Name,
                    referencedProjectName,
                    RuleName,
                    $"Project of type '{_projectType}' cannot reference '{referencedProjectName}'. This reference is not in the allowed list.",
                    ViolationSeverity.Error,
                    suggestion,
                    "https://github.com/n2jsoft/modularguard/blob/main/docs/rules.md",
                    IsAutoFixable: true,
                    FilePath: reference.FilePath,
                    LineNumber: reference.LineNumber,
                    ColumnNumber: reference.ColumnNumber);
            }
        }
    }

    private bool IsExplicitlyAllowed(ModuleInfo sourceModule, ModuleInfo referencedModule)
    {
        foreach (var allowedPattern in _ruleConfig.Allowed)
        {
            if (MatchesPattern(sourceModule, referencedModule, allowedPattern))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsExplicitlyDenied(ModuleInfo sourceModule, ModuleInfo referencedModule)
    {
        foreach (var deniedPattern in _ruleConfig.Denied)
        {
            if (MatchesPattern(sourceModule, referencedModule, deniedPattern))
            {
                return true;
            }
        }

        return false;
    }

    private bool MatchesPattern(ModuleInfo sourceModule, ModuleInfo referencedModule, string pattern)
    {
        // Handle special patterns
        if (pattern.Contains("{module}"))
        {
            // Replace {module} with the source module's name
            var expandedPattern = pattern.Replace("{module}", sourceModule.ModuleName);
            return MatchesGlobPattern(referencedModule.ProjectInfo.Name, expandedPattern);
        }

        // Handle wildcard patterns
        if (pattern.Contains("*"))
        {
            return MatchesGlobPattern(referencedModule.ProjectInfo.Name, pattern);
        }

        // Exact match
        return referencedModule.ProjectInfo.Name.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesGlobPattern(string projectName, string pattern)
    {
        try
        {
            // Convert glob pattern to regex
            var regexPattern = Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".");

            var regex = new Regex($"^{regexPattern}$", RegexOptions.IgnoreCase);
            return regex.IsMatch(projectName);
        }
        catch
        {
            return false;
        }
    }

    private string GetProjectTypeString(ModuleInfo module)
    {
        // Try to find matching project type from configuration
        var detector = new ConfigurableProjectTypeDetector(_configuration);
        return detector.DetectProjectType(module.ProjectInfo.Name);
    }

    private string GenerateSuggestion(ModuleInfo sourceModule, ModuleInfo referencedModule)
    {
        var suggestions = new List<string>();

        // Suggest removing the invalid reference
        suggestions.Add(
            $"Remove the reference to '{referencedModule.ProjectInfo.Name}' from '{sourceModule.ProjectInfo.Name}'");

        // Suggest alternative references based on allowed patterns
        if (_ruleConfig.Allowed.Count > 0)
        {
            var allowedExamples = _ruleConfig.Allowed
                .Take(3)
                .Select(pattern => pattern.Replace("{module}", sourceModule.ModuleName))
                .ToList();

            if (allowedExamples.Count > 0)
            {
                suggestions.Add($"Allowed references for {_projectType}: {string.Join(", ", allowedExamples)}");
            }
        }

        // Specific suggestions based on common patterns
        if (referencedModule.Type.Contains("Endpoints") && sourceModule.Type.Contains("App"))
        {
            suggestions.Add(
                "App projects should not reference Endpoints projects. Consider moving shared logic to Core or Infrastructure.");
        }
        else if (referencedModule.Type.Contains("Infrastructure") && sourceModule.Type.Contains("Core"))
        {
            suggestions.Add(
                "Core projects should not reference Infrastructure projects. Core should be infrastructure-agnostic.");
        }
        else if (referencedModule.Type.Contains("App") && sourceModule.Type.Contains("Infrastructure"))
        {
            suggestions.Add(
                "Infrastructure projects should not reference App projects. Consider moving shared logic to Core.");
        }

        return string.Join(" | ", suggestions);
    }
}