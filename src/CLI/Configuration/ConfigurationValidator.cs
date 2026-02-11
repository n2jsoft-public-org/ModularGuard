namespace n2jSoft.ModularGuard.CLI.Configuration;

/// <summary>
///     Validates LinterConfiguration for correctness and provides detailed error messages
/// </summary>
public sealed class ConfigurationValidator
{
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();

    /// <summary>
    ///     Validates a configuration and returns validation result
    /// </summary>
    public ValidationResult Validate(LinterConfiguration configuration)
    {
        _errors.Clear();
        _warnings.Clear();

        ValidateModulePatterns(configuration.Modules);
        ValidateSharedPatterns(configuration.Shared);
        ValidateDependencyRules(configuration.DependencyRules, configuration.Modules, configuration.Shared);
        ValidateSeverityOverrides(configuration.SeverityOverrides);
        ValidateIgnoredProjects(configuration.IgnoredProjects);

        return new ValidationResult(_errors, _warnings);
    }

    private void ValidateModulePatterns(ModuleConfiguration modules)
    {
        if (modules.Patterns.Count == 0)
        {
            _warnings.Add("Module configuration has no patterns defined. No module projects will be detected.");
            return;
        }

        var seenTypes = new HashSet<string>();
        var seenPatterns = new HashSet<string>();

        foreach (var pattern in modules.Patterns)
        {
            ValidateProjectPattern(pattern, "modules", seenTypes, seenPatterns);

            if (string.IsNullOrWhiteSpace(pattern.ModuleExtraction))
            {
                _errors.Add(
                    $"Module pattern '{pattern.Name}' (type: {pattern.Type}) must have a moduleExtraction regex to extract module name.");
            }
        }
    }

    private void ValidateSharedPatterns(SharedConfiguration shared)
    {
        if (shared.Patterns.Count == 0)
        {
            _warnings.Add("Shared configuration has no patterns defined. No shared projects will be detected.");
            return;
        }

        var seenTypes = new HashSet<string>();
        var seenPatterns = new HashSet<string>();

        foreach (var pattern in shared.Patterns)
        {
            ValidateProjectPattern(pattern, "shared", seenTypes, seenPatterns);
        }

        if (!string.IsNullOrWhiteSpace(shared.WorkingDirectory))
        {
            if (shared.WorkingDirectory.Contains('*') || shared.WorkingDirectory.Contains('?'))
            {
                _errors.Add($"Shared workingDirectory cannot contain wildcards: '{shared.WorkingDirectory}'");
            }
        }
    }

    private void ValidateProjectPattern(ProjectPattern pattern, string section, HashSet<string> seenTypes,
        HashSet<string> seenPatterns)
    {
        if (string.IsNullOrWhiteSpace(pattern.Name))
        {
            _errors.Add($"Project pattern in {section} has empty name.");
        }

        if (string.IsNullOrWhiteSpace(pattern.Pattern))
        {
            _errors.Add($"Project pattern '{pattern.Name}' in {section} has empty pattern.");
        }
        else if (seenPatterns.Contains(pattern.Pattern))
        {
            _errors.Add($"Duplicate pattern '{pattern.Pattern}' found in {section}. Each pattern must be unique.");
        }
        else
        {
            seenPatterns.Add(pattern.Pattern);
        }

        if (string.IsNullOrWhiteSpace(pattern.Type))
        {
            _errors.Add($"Project pattern '{pattern.Name}' in {section} has empty type identifier.");
        }
        else if (seenTypes.Contains(pattern.Type))
        {
            _errors.Add($"Duplicate type '{pattern.Type}' found in {section}. Each type must be unique.");
        }
        else
        {
            seenTypes.Add(pattern.Type);
        }
    }

    private void ValidateDependencyRules(
        Dictionary<string, DependencyRuleConfiguration> rules,
        ModuleConfiguration modules,
        SharedConfiguration shared)
    {
        if (rules.Count == 0)
        {
            _warnings.Add("No dependency rules defined. All project dependencies will be allowed.");
            return;
        }

        var moduleTypes = modules.Patterns.Select(p => p.Type).ToHashSet();
        var sharedTypes = shared.Patterns.Select(p => p.Type).ToHashSet();
        var allTypes = moduleTypes.Concat(sharedTypes).ToHashSet();

        // Check for rules without corresponding project types
        foreach (var (type, rule) in rules)
        {
            if (!allTypes.Contains(type))
            {
                _warnings.Add(
                    $"Dependency rule defined for type '{type}' but no project pattern with this type exists.");
            }

            if (rule.Allowed.Count == 0 && rule.Denied.Count == 0)
            {
                _warnings.Add(
                    $"Dependency rule for type '{type}' has no allowed or denied patterns. This rule has no effect.");
            }

            ValidateDependencyPatterns(type, rule.Allowed, "allowed");
            ValidateDependencyPatterns(type, rule.Denied, "denied");
        }

        // Check for project types without rules
        foreach (var type in allTypes)
        {
            if (!rules.ContainsKey(type))
            {
                // Check if warnings are enabled for this type
                var isModuleType = moduleTypes.Contains(type);
                var isSharedType = sharedTypes.Contains(type);

                var shouldWarn = (isModuleType && modules.MissingRulesWarnings) ||
                                (isSharedType && shared.MissingRulesWarnings);

                if (shouldWarn)
                {
                    _warnings.Add(
                        $"Project type '{type}' has no dependency rules defined. All dependencies will be allowed for this type.");
                }
            }
        }
    }

    private void ValidateDependencyPatterns(string ruleType, List<string> patterns, string category)
    {
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                _errors.Add($"Dependency rule for type '{ruleType}' contains empty {category} pattern.");
            }
        }
    }

    private void ValidateSeverityOverrides(List<SeverityOverride> overrides)
    {
        var validSeverities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Error", "Warning", "Info"
        };

        var seenRules = new HashSet<string>();

        foreach (var @override in overrides)
        {
            if (string.IsNullOrWhiteSpace(@override.Rule))
            {
                _errors.Add("Severity override has empty rule name.");
                continue;
            }

            if (seenRules.Contains(@override.Rule))
            {
                _warnings.Add(
                    $"Duplicate severity override for rule '{@override.Rule}'. Only the last override will apply.");
            }
            else
            {
                seenRules.Add(@override.Rule);
            }

            if (string.IsNullOrWhiteSpace(@override.Severity))
            {
                _errors.Add($"Severity override for rule '{@override.Rule}' has empty severity.");
            }
            else if (!validSeverities.Contains(@override.Severity))
            {
                _errors.Add(
                    $"Severity override for rule '{@override.Rule}' has invalid severity '{@override.Severity}'. Valid values are: Error, Warning, Info.");
            }
        }
    }

    private void ValidateIgnoredProjects(List<string> ignoredProjects)
    {
        foreach (var pattern in ignoredProjects)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                _errors.Add("Ignored projects list contains empty pattern.");
            }
        }
    }
}

/// <summary>
///     Result of configuration validation
/// </summary>
public sealed class ValidationResult
{
    public ValidationResult(IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
    {
        Errors = errors;
        Warnings = warnings;
    }

    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyList<string> Warnings { get; }
    public bool IsValid => Errors.Count == 0;
}