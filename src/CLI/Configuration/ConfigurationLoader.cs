using System.Text.Json;
using n2jSoft.ModularGuard.CLI.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace n2jSoft.ModularGuard.CLI.Configuration;

public sealed class ConfigurationLoader
{
    private static readonly string[] ConfigFileNames =
    {
        ".modularguard.yml",
        ".modularguard.yaml",
        ".modularguard.json",
        "modularguard.yml",
        "modularguard.yaml",
        "modularguard.json"
    };

    private readonly HashSet<string> _loadedFiles = new();
    private readonly ConfigurationMerger _merger = new();

    private readonly ConfigurationValidator _validator = new();

    /// <summary>
    ///     Discovers and loads configuration file from a directory
    /// </summary>
    public LinterConfiguration? TryLoadConfiguration(string searchDirectory, string? profile = null)
    {
        var configFilePath = DiscoverConfigFile(searchDirectory);
        if (configFilePath == null)
        {
            return null;
        }

        return LoadConfigurationFromFile(configFilePath, profile);
    }

    /// <summary>
    ///     Loads configuration from a specific file with optional profile
    /// </summary>
    public LinterConfiguration LoadConfigurationFromFile(string filePath, string? profile = null)
    {
        _loadedFiles.Clear(); // Reset for each top-level load
        var configuration = LoadConfigurationFromFileInternal(filePath);

        // Apply profile if specified
        if (!string.IsNullOrWhiteSpace(profile))
        {
            configuration = ApplyProfile(configuration, profile);
        }

        return configuration;
    }

    private LinterConfiguration ApplyProfile(LinterConfiguration baseConfig, string profileName)
    {
        if (!baseConfig.Profiles.TryGetValue(profileName, out var profile))
        {
            throw new InvalidOperationException(
                $"Profile '{profileName}' not found in configuration. Available profiles: {string.Join(", ", baseConfig.Profiles.Keys)}");
        }

        // Merge profile settings into base configuration
        return new LinterConfiguration
        {
            Extends = null,
            Modules = baseConfig.Modules,
            Shared = baseConfig.Shared,
            DependencyRules = MergeProfileRules(baseConfig.DependencyRules, profile.DependencyRules),
            IgnoredProjects = baseConfig.IgnoredProjects.Concat(profile.IgnoredProjects).Distinct().ToList(),
            SeverityOverrides = MergeProfileSeverityOverrides(baseConfig.SeverityOverrides, profile.SeverityOverrides),
            Profiles = baseConfig.Profiles // Keep profiles for documentation
        };
    }

    private Dictionary<string, DependencyRuleConfiguration> MergeProfileRules(
        Dictionary<string, DependencyRuleConfiguration> baseRules,
        Dictionary<string, DependencyRuleConfiguration> profileRules)
    {
        var merged = new Dictionary<string, DependencyRuleConfiguration>(baseRules);

        foreach (var (type, rule) in profileRules)
        {
            merged[type] = rule; // Profile rules override base rules
        }

        return merged;
    }

    private List<SeverityOverride> MergeProfileSeverityOverrides(
        List<SeverityOverride> baseOverrides,
        List<SeverityOverride> profileOverrides)
    {
        var merged = new Dictionary<string, SeverityOverride>();

        foreach (var @override in baseOverrides)
        {
            merged[@override.Rule] = @override;
        }

        foreach (var @override in profileOverrides)
        {
            merged[@override.Rule] = @override; // Profile overrides base
        }

        return merged.Values.ToList();
    }

    private LinterConfiguration LoadConfigurationFromFileInternal(string filePath)
    {
        // Handle special "default" keyword
        if (filePath == "::default::")
        {
            return CreateDefaultConfiguration();
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {filePath}");
        }

        var absolutePath = Path.GetFullPath(filePath);

        // Detect circular dependencies
        if (_loadedFiles.Contains(absolutePath))
        {
            throw new InvalidOperationException(
                $"Circular configuration dependency detected: '{filePath}' has already been loaded.");
        }

        _loadedFiles.Add(absolutePath);

        var content = File.ReadAllText(filePath);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        LinterConfiguration configuration;
        try
        {
            configuration = extension switch
            {
                ".yml" or ".yaml" => LoadYaml(content),
                ".json" => LoadJson(content),
                _ => throw new NotSupportedException($"Unsupported configuration file format: {extension}")
            };
        }
        catch (Exception ex) when (ex is not NotSupportedException)
        {
            throw new InvalidOperationException(
                $"Failed to parse configuration file '{filePath}': {ex.Message}",
                ex);
        }

        // Handle inheritance
        if (!string.IsNullOrWhiteSpace(configuration.Extends))
        {
            var baseFilePath = ResolveConfigPath(configuration.Extends, Path.GetDirectoryName(absolutePath)!);
            var baseConfig = LoadConfigurationFromFileInternal(baseFilePath);
            configuration = _merger.Merge(baseConfig, configuration);
        }

        ValidateConfiguration(configuration, filePath);
        return configuration;
    }

    private string ResolveConfigPath(string extendsPath, string currentDirectory)
    {
        // Support relative paths and special keywords
        if (extendsPath == "default")
        {
            // Special keyword to extend default configuration
            return "::default::";
        }

        if (Path.IsPathRooted(extendsPath))
        {
            return extendsPath;
        }

        return Path.Combine(currentDirectory, extendsPath);
    }

    private void ValidateConfiguration(LinterConfiguration configuration, string filePath)
    {
        var validationResult = _validator.Validate(configuration);

        if (!validationResult.IsValid)
        {
            var errorMessage = $"Configuration file '{filePath}' contains errors:\n";
            foreach (var error in validationResult.Errors)
            {
                errorMessage += $"  - {error}\n";
            }

            throw new InvalidOperationException(errorMessage.TrimEnd());
        }

        if (validationResult.Warnings.Count > 0)
        {
            Console.WriteLine($"Configuration warnings in '{filePath}':");
            foreach (var warning in validationResult.Warnings)
            {
                Console.WriteLine($"  - {warning}");
            }

            Console.WriteLine();
        }
    }

    /// <summary>
    ///     Creates default configuration with hardcoded modular monolith rules
    /// </summary>
    public LinterConfiguration CreateDefaultConfiguration()
    {
        return new LinterConfiguration
        {
            Modules = new ModuleConfiguration
            {
                MissingRulesWarnings = true,
                Patterns = new List<ProjectPattern>
                {
                    new() { Name = "Core", Pattern = "*.Core", Type = "core", ModuleExtraction = "^(.+)\\.Core$" },
                    new()
                    {
                        Name = "Infrastructure", Pattern = "*.Infrastructure", Type = "infrastructure",
                        ModuleExtraction = "^(.+)\\.Infrastructure$"
                    },
                    new()
                    {
                        Name = "AdminApp", Pattern = "*.Admin.App", Type = "admin-app",
                        ModuleExtraction = "^(.+)\\.Admin\\.App$"
                    },
                    new()
                    {
                        Name = "AdminEndpoints", Pattern = "*.Admin.Endpoints", Type = "admin-endpoints",
                        ModuleExtraction = "^(.+)\\.Admin\\.Endpoints$"
                    },
                    new()
                    {
                        Name = "PrivateApp", Pattern = "*.Private.App", Type = "private-app",
                        ModuleExtraction = "^(.+)\\.Private\\.App$"
                    },
                    new()
                    {
                        Name = "PrivateEndpoints", Pattern = "*.Private.Endpoints", Type = "private-endpoints",
                        ModuleExtraction = "^(.+)\\.Private\\.Endpoints$"
                    },
                    new()
                    {
                        Name = "PublicApp", Pattern = "*.Public.App", Type = "public-app",
                        ModuleExtraction = "^(.+)\\.Public\\.App$"
                    },
                    new()
                    {
                        Name = "PublicEndpoints", Pattern = "*.Public.Endpoints", Type = "public-endpoints",
                        ModuleExtraction = "^(.+)\\.Public\\.Endpoints$"
                    },
                    new()
                    {
                        Name = "SharedEvents", Pattern = "*.Shared.Events", Type = "shared-events",
                        ModuleExtraction = "^(.+)\\.Shared\\.Events$"
                    },
                    new()
                    {
                        Name = "SharedMessages", Pattern = "*.Shared.Messages", Type = "shared-messages",
                        ModuleExtraction = "^(.+)\\.Shared\\.Messages$"
                    }
                }
            },
            Shared = new SharedConfiguration
            {
                MissingRulesWarnings = true,
                Patterns = new List<ProjectPattern>
                {
                    new() { Name = "SharedCore", Pattern = "Shared.Core", Type = "shared-core" },
                    new()
                    {
                        Name = "SharedInfrastructure", Pattern = "Shared.Infrastructure", Type = "shared-infrastructure"
                    },
                    new() { Name = "SharedAppAdmin", Pattern = "Shared.App.Admin", Type = "shared-app-admin" },
                    new() { Name = "SharedAppPrivate", Pattern = "Shared.App.Private", Type = "shared-app-private" },
                    new() { Name = "SharedAppPublic", Pattern = "Shared.App.Public", Type = "shared-app-public" }
                }
            },
            DependencyRules = new Dictionary<string, DependencyRuleConfiguration>
            {
                ["core"] = new()
                {
                    Allowed = new List<string> { "Shared.Core" },
                    Denied = new List<string> { "*.Infrastructure", "*.App", "*.Endpoints" }
                },
                ["infrastructure"] = new()
                {
                    Allowed = new List<string> { "Shared.Infrastructure", "{module}.Core" },
                    Denied = new List<string> { "*.App", "*.Endpoints" }
                },
                ["admin-app"] = new()
                {
                    Allowed = new List<string>
                    {
                        "Shared.App.Admin",
                        "{module}.Core",
                        "{module}.Infrastructure",
                        "*.Shared.Events",
                        "*.Shared.Messages"
                    },
                    Denied = new List<string> { "*.Endpoints" }
                },
                ["private-app"] = new()
                {
                    Allowed = new List<string>
                    {
                        "Shared.App.Private",
                        "{module}.Core",
                        "{module}.Infrastructure",
                        "*.Shared.Events",
                        "*.Shared.Messages"
                    },
                    Denied = new List<string> { "*.Endpoints" }
                },
                ["public-app"] = new()
                {
                    Allowed = new List<string>
                    {
                        "Shared.App.Public",
                        "{module}.Core",
                        "{module}.Infrastructure",
                        "*.Shared.Events",
                        "*.Shared.Messages"
                    },
                    Denied = new List<string> { "*.Endpoints" }
                },
                ["admin-endpoints"] = new()
                {
                    Allowed = new List<string> { "{module}.Admin.App" },
                    Denied = new List<string> { "*.Core", "*.Infrastructure" }
                },
                ["private-endpoints"] = new()
                {
                    Allowed = new List<string> { "{module}.Private.App" },
                    Denied = new List<string> { "*.Core", "*.Infrastructure" }
                },
                ["public-endpoints"] = new()
                {
                    Allowed = new List<string> { "{module}.Public.App" },
                    Denied = new List<string> { "*.Core", "*.Infrastructure" }
                }
            },
            IgnoredProjects = new List<string>(),
            SeverityOverrides = new List<SeverityOverride>()
        };
    }

    private string? DiscoverConfigFile(string directory)
    {
        foreach (var fileName in ConfigFileNames)
        {
            var filePath = Path.Combine(directory, fileName);
            if (File.Exists(filePath))
            {
                return filePath;
            }
        }

        return null;
    }

    private LinterConfiguration LoadYaml(string content)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        return deserializer.Deserialize<LinterConfiguration>(content);
    }

    private LinterConfiguration LoadJson(string content)
    {
        return JsonSerializer.Deserialize(content, AppJsonSerializerContext.Default.LinterConfiguration)
               ?? throw new InvalidOperationException("Failed to deserialize configuration");
    }
}