namespace n2jSoft.ModularGuard.CLI.Configuration;

/// <summary>
///     Root configuration for the modular monolith linter
/// </summary>
public sealed class LinterConfiguration
{
    /// <summary>
    ///     Base configuration file to extend (optional)
    /// </summary>
    public string? Extends { get; init; }

    /// <summary>
    ///     Module project configuration (e.g., Module1.Core, Module2.Infrastructure)
    /// </summary>
    public ModuleConfiguration Modules { get; init; } = new();

    /// <summary>
    ///     Shared project configuration (e.g., Shared.Core, Shared.Infrastructure)
    /// </summary>
    public SharedConfiguration Shared { get; init; } = new();

    /// <summary>
    ///     Dependency rules per project type
    /// </summary>
    public Dictionary<string, DependencyRuleConfiguration> DependencyRules { get; init; } = new();

    /// <summary>
    ///     Projects to ignore during validation
    /// </summary>
    public List<string> IgnoredProjects { get; init; } = new();

    /// <summary>
    ///     Severity overrides for specific rules
    /// </summary>
    public List<SeverityOverride> SeverityOverrides { get; init; } = new();

    /// <summary>
    ///     Environment-specific configuration profiles
    /// </summary>
    public Dictionary<string, ConfigurationProfile> Profiles { get; init; } = new();
}

/// <summary>
///     Configuration for module projects
/// </summary>
public sealed class ModuleConfiguration
{
    /// <summary>
    ///     List of module project patterns to identify project types
    /// </summary>
    public List<ProjectPattern> Patterns { get; init; } = new();

    /// <summary>
    ///     Whether to warn about module project types without dependency rules (default: true)
    /// </summary>
    public bool MissingRulesWarnings { get; init; } = true;
}

/// <summary>
///     Configuration for shared projects
/// </summary>
public sealed class SharedConfiguration
{
    /// <summary>
    ///     Optional working directory for shared projects (e.g., "src/Shared").
    ///     When set, only projects within this directory will match shared patterns,
    ///     and this directory will be excluded from module pattern matching.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    ///     List of shared project patterns to identify project types
    /// </summary>
    public List<ProjectPattern> Patterns { get; init; } = new();

    /// <summary>
    ///     Whether to warn about shared project types without dependency rules (default: true)
    /// </summary>
    public bool MissingRulesWarnings { get; init; } = true;
}

/// <summary>
///     A pattern to identify a project type
/// </summary>
public sealed class ProjectPattern
{
    /// <summary>
    ///     Display name for this project type
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     Glob or regex pattern to match project names
    /// </summary>
    public string Pattern { get; init; } = string.Empty;

    /// <summary>
    ///     Unique type identifier
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    ///     Regex pattern to extract module name from project name
    /// </summary>
    public string? ModuleExtraction { get; init; }
}

/// <summary>
///     Dependency rules for a project type
/// </summary>
public sealed class DependencyRuleConfiguration
{
    /// <summary>
    ///     Inherit behavior: replace (default), merge, or extend
    /// </summary>
    public string Inherit { get; init; } = "replace";

    /// <summary>
    ///     List of allowed dependency patterns
    /// </summary>
    public List<string> Allowed { get; init; } = new();

    /// <summary>
    ///     List of denied dependency patterns
    /// </summary>
    public List<string> Denied { get; init; } = new();
}

/// <summary>
///     Override severity for a specific rule
/// </summary>
public sealed class SeverityOverride
{
    /// <summary>
    ///     Rule name to override
    /// </summary>
    public string Rule { get; init; } = string.Empty;

    /// <summary>
    ///     New severity level (Error, Warning, Info)
    /// </summary>
    public string Severity { get; init; } = string.Empty;
}

/// <summary>
///     Environment-specific configuration profile
/// </summary>
public sealed class ConfigurationProfile
{
    /// <summary>
    ///     Additional projects to ignore in this profile
    /// </summary>
    public List<string> IgnoredProjects { get; init; } = new();

    /// <summary>
    ///     Profile-specific severity overrides
    /// </summary>
    public List<SeverityOverride> SeverityOverrides { get; init; } = new();

    /// <summary>
    ///     Profile-specific dependency rule overrides
    /// </summary>
    public Dictionary<string, DependencyRuleConfiguration> DependencyRules { get; init; } = new();
}