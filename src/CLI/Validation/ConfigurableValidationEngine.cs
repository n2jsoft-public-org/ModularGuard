using System.Text.RegularExpressions;
using n2jSoft.ModularGuard.CLI.Configuration;
using n2jSoft.ModularGuard.CLI.Models;
using n2jSoft.ModularGuard.CLI.Validation.Rules;
using Spectre.Console;

namespace n2jSoft.ModularGuard.CLI.Validation;

/// <summary>
///     Validation engine that generates rules dynamically from configuration
/// </summary>
public sealed class ConfigurableValidationEngine
{
    private readonly LinterConfiguration _configuration;
    private readonly List<IValidationRule> _rules;
    private readonly bool _verbose;

    public ConfigurableValidationEngine(LinterConfiguration configuration, bool verbose = false)
    {
        _configuration = configuration;
        _verbose = verbose;
        _rules = GenerateRulesFromConfiguration();

        if (_verbose)
        {
            AnsiConsole.MarkupLine("[dim]Initialized validation engine with {0} rules[/]", _rules.Count);
        }
    }

    public IReadOnlyList<Violation> ValidateAll(IReadOnlyList<ModuleInfo> modules)
    {
        var violations = new List<Violation>();

        if (_verbose)
        {
            AnsiConsole.MarkupLine("[dim]Validating {0} modules...[/]", modules.Count);
        }

        foreach (var module in modules)
        {
            // Skip ignored projects
            if (IsIgnored(module.ProjectInfo.Name))
            {
                if (_verbose)
                {
                    AnsiConsole.MarkupLine("[dim]Skipping ignored module: {0}[/]", module.ProjectInfo.Name);
                }

                continue;
            }

            var applicableRules = _rules.Where(r => r.AppliesTo(module)).ToList();

            if (_verbose)
            {
                AnsiConsole.MarkupLine("[dim]Validating {0} ({1}) with {2} applicable rule(s)[/]",
                    module.ProjectInfo.Name,
                    module.Type,
                    applicableRules.Count);
            }

            foreach (var rule in applicableRules)
            {
                var ruleViolations = rule.Validate(module, modules).ToList();

                if (_verbose && ruleViolations.Any())
                {
                    AnsiConsole.MarkupLine("[dim]  Rule '{0}' found {1} violation(s)[/]",
                        rule.GetType().Name,
                        ruleViolations.Count);
                }

                violations.AddRange(ruleViolations);
            }
        }

        // Apply severity overrides
        violations = ApplySeverityOverrides(violations);

        if (_verbose && violations.Any())
        {
            AnsiConsole.WriteLine($"Applied severity overrides, total violations: {violations.Count}");
        }

        return violations;
    }

    public ValidationResult ValidateAllWithResult(IReadOnlyList<ModuleInfo> modules)
    {
        var violations = ValidateAll(modules);
        return new ValidationResult(violations);
    }

    private List<IValidationRule> GenerateRulesFromConfiguration()
    {
        var rules = new List<IValidationRule>
        {
            // Always add UnknownProjectTypeRule first to catch unqualified projects
            new UnknownProjectTypeRule()
        };

        // Generate a rule for each project type that has dependency rules
        foreach (var (projectType, ruleConfig) in _configuration.DependencyRules)
        {
            rules.Add(new ConfigurableValidationRule(projectType, ruleConfig, _configuration));
        }

        return rules;
    }

    private bool IsIgnored(string projectName)
    {
        foreach (var ignoredPattern in _configuration.IgnoredProjects)
        {
            if (MatchesGlobPattern(projectName, ignoredPattern))
            {
                return true;
            }
        }

        return false;
    }

    private bool MatchesGlobPattern(string projectName, string pattern)
    {
        try
        {
            var regexPattern = Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".");

            var regex = new Regex($"^{regexPattern}$",
                RegexOptions.IgnoreCase);

            return regex.IsMatch(projectName);
        }
        catch
        {
            return false;
        }
    }

    private List<Violation> ApplySeverityOverrides(List<Violation> violations)
    {
        if (_configuration.SeverityOverrides.Count == 0)
        {
            return violations;
        }

        var result = new List<Violation>();

        foreach (var violation in violations)
        {
            var @override = _configuration.SeverityOverrides
                .FirstOrDefault(o => o.Rule.Equals(violation.RuleName, StringComparison.OrdinalIgnoreCase));

            if (@override != null && Enum.TryParse<ViolationSeverity>(@override.Severity, true, out var newSeverity))
            {
                result.Add(violation with { Severity = newSeverity });
            }
            else
            {
                result.Add(violation);
            }
        }

        return result;
    }
}