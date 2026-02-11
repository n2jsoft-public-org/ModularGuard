namespace n2jSoft.ModularGuard.CLI.Configuration;

/// <summary>
///     Merges configurations to support inheritance
/// </summary>
public sealed class ConfigurationMerger
{
    /// <summary>
    ///     Merges child configuration with base configuration
    /// </summary>
    public LinterConfiguration Merge(LinterConfiguration baseConfig, LinterConfiguration childConfig)
    {
        return new LinterConfiguration
        {
            Extends = null, // Remove extends chain after merging
            Modules = MergeModules(baseConfig.Modules, childConfig.Modules),
            Shared = MergeShared(baseConfig.Shared, childConfig.Shared),
            DependencyRules = MergeDependencyRules(baseConfig.DependencyRules, childConfig.DependencyRules),
            IgnoredProjects = MergeLists(baseConfig.IgnoredProjects, childConfig.IgnoredProjects),
            SeverityOverrides = MergeSeverityOverrides(baseConfig.SeverityOverrides, childConfig.SeverityOverrides)
        };
    }

    private ModuleConfiguration MergeModules(ModuleConfiguration baseConfig, ModuleConfiguration childConfig)
    {
        // Child patterns replace base patterns
        if (childConfig.Patterns.Count > 0)
        {
            return childConfig;
        }

        return baseConfig;
    }

    private SharedConfiguration MergeShared(SharedConfiguration baseConfig, SharedConfiguration childConfig)
    {
        // Child patterns replace base patterns
        if (childConfig.Patterns.Count > 0)
        {
            return new SharedConfiguration
            {
                WorkingDirectory = childConfig.WorkingDirectory ?? baseConfig.WorkingDirectory,
                Patterns = childConfig.Patterns
            };
        }

        return baseConfig;
    }

    private Dictionary<string, DependencyRuleConfiguration> MergeDependencyRules(
        Dictionary<string, DependencyRuleConfiguration> baseRules,
        Dictionary<string, DependencyRuleConfiguration> childRules)
    {
        var merged = new Dictionary<string, DependencyRuleConfiguration>(baseRules);

        foreach (var (type, childRule) in childRules)
        {
            if (!merged.TryGetValue(type, out var baseRule))
            {
                // New rule in child, add it
                merged[type] = childRule;
                continue;
            }

            // Merge based on inherit strategy
            merged[type] = childRule.Inherit.ToLowerInvariant() switch
            {
                "merge" => MergeRules(baseRule, childRule),
                "extend" => ExtendRules(baseRule, childRule),
                _ => childRule // "replace" is default
            };
        }

        return merged;
    }

    private DependencyRuleConfiguration MergeRules(
        DependencyRuleConfiguration baseRule,
        DependencyRuleConfiguration childRule)
    {
        // Merge: combine allowed and denied lists, removing duplicates
        return new DependencyRuleConfiguration
        {
            Inherit = "replace", // Reset inherit after merging
            Allowed = baseRule.Allowed.Concat(childRule.Allowed).Distinct().ToList(),
            Denied = baseRule.Denied.Concat(childRule.Denied).Distinct().ToList()
        };
    }

    private DependencyRuleConfiguration ExtendRules(
        DependencyRuleConfiguration baseRule,
        DependencyRuleConfiguration childRule)
    {
        // Extend: child only adds to base, doesn't remove
        var allowed = new List<string>(baseRule.Allowed);
        var denied = new List<string>(baseRule.Denied);

        foreach (var pattern in childRule.Allowed)
        {
            if (!allowed.Contains(pattern))
            {
                allowed.Add(pattern);
            }
        }

        foreach (var pattern in childRule.Denied)
        {
            if (!denied.Contains(pattern))
            {
                denied.Add(pattern);
            }
        }

        return new DependencyRuleConfiguration
        {
            Inherit = "replace", // Reset inherit after merging
            Allowed = allowed,
            Denied = denied
        };
    }

    private List<string> MergeLists(List<string> baseList, List<string> childList)
    {
        // Combine and remove duplicates
        return baseList.Concat(childList).Distinct().ToList();
    }

    private List<SeverityOverride> MergeSeverityOverrides(
        List<SeverityOverride> baseOverrides,
        List<SeverityOverride> childOverrides)
    {
        var merged = new Dictionary<string, SeverityOverride>();

        foreach (var @override in baseOverrides)
        {
            merged[@override.Rule] = @override;
        }

        foreach (var @override in childOverrides)
        {
            merged[@override.Rule] = @override; // Child overrides base
        }

        return merged.Values.ToList();
    }
}