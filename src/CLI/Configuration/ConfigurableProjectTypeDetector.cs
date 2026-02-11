using System.Text.RegularExpressions;

namespace n2jSoft.ModularGuard.CLI.Configuration;

/// <summary>
///     Detects project types based on configuration patterns
/// </summary>
public sealed class ConfigurableProjectTypeDetector
{
    private readonly LinterConfiguration _configuration;
    private readonly List<CompiledPattern> _modulePatterns;
    private readonly List<CompiledPattern> _sharedPatterns;
    private readonly string? _sharedWorkingDirectory;

    public ConfigurableProjectTypeDetector(LinterConfiguration configuration)
    {
        _configuration = configuration;
        _modulePatterns = CompilePatterns(configuration.Modules.Patterns);
        _sharedPatterns = CompilePatterns(configuration.Shared.Patterns);
        _sharedWorkingDirectory = NormalizeDirectory(configuration.Shared.WorkingDirectory);
    }

    /// <summary>
    ///     Detects project type based on configured patterns (name-only matching)
    /// </summary>
    public string DetectProjectType(string projectName)
    {
        // Check shared patterns first (they are more specific - exact matches)
        // This ensures "Shared.Core" matches shared-core before *.Core
        foreach (var pattern in _sharedPatterns)
        {
            if (pattern.Regex.IsMatch(projectName))
            {
                return pattern.Type;
            }
        }

        // Check module patterns second (they use wildcards)
        foreach (var pattern in _modulePatterns)
        {
            if (pattern.Regex.IsMatch(projectName))
            {
                return pattern.Type;
            }
        }

        return "unknown";
    }

    /// <summary>
    ///     Detects project type based on configured patterns with directory-aware filtering
    /// </summary>
    public string DetectProjectType(string projectName, string projectFilePath, string solutionRootPath)
    {
        // If working directory is not configured, use name-only matching
        if (string.IsNullOrEmpty(_sharedWorkingDirectory))
        {
            return DetectProjectType(projectName);
        }

        // Normalize paths for comparison
        var normalizedProjectPath = NormalizePath(projectFilePath);
        var normalizedRootPath = NormalizePath(solutionRootPath);
        var sharedDirectory = NormalizePath(Path.Combine(normalizedRootPath, _sharedWorkingDirectory));

        // Determine if project is inside shared working directory
        // Add trailing separator to ensure we're checking directory boundaries
        var sharedDirectoryWithSeparator = sharedDirectory.TrimEnd('/') + "/";
        var isInSharedDirectory = normalizedProjectPath.StartsWith(sharedDirectoryWithSeparator, StringComparison.OrdinalIgnoreCase);

        if (isInSharedDirectory)
        {
            // Only check shared patterns for projects inside shared directory
            foreach (var pattern in _sharedPatterns)
            {
                if (pattern.Regex.IsMatch(projectName))
                {
                    return pattern.Type;
                }
            }
        }
        else
        {
            // Only check module patterns for projects outside shared directory
            foreach (var pattern in _modulePatterns)
            {
                if (pattern.Regex.IsMatch(projectName))
                {
                    return pattern.Type;
                }
            }
        }

        return "unknown";
    }

    /// <summary>
    ///     Extracts module name from project name using configured extraction pattern
    /// </summary>
    public string ExtractModuleName(string projectName, string projectType)
    {
        if (projectType == "unknown")
        {
            return "Unknown";
        }

        // Check if this is a shared project - shared projects always belong to "Shared" module
        var sharedPattern = _sharedPatterns.FirstOrDefault(p => p.Type == projectType);
        if (sharedPattern != null)
        {
            return "Shared";
        }

        // Find the module pattern for this project type
        var modulePattern = _configuration.Modules.Patterns
            .FirstOrDefault(p => p.Type == projectType);

        if (modulePattern?.ModuleExtraction != null)
        {
            try
            {
                var regex = new Regex(modulePattern.ModuleExtraction);
                var match = regex.Match(projectName);

                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }
            catch (Exception)
            {
                // If regex fails, fall back to default extraction
            }
        }

        // Default: extract first part before first dot
        var firstDotIndex = projectName.IndexOf('.');
        return firstDotIndex > 0 ? projectName[..firstDotIndex] : projectName;
    }

    private List<CompiledPattern> CompilePatterns(List<ProjectPattern> patterns)
    {
        var compiled = new List<CompiledPattern>();

        foreach (var pattern in patterns)
        {
            try
            {
                // Convert glob pattern to regex
                var regexPattern = ConvertGlobToRegex(pattern.Pattern);
                var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);

                compiled.Add(new CompiledPattern
                {
                    Type = pattern.Type,
                    Regex = regex,
                    OriginalPattern = pattern.Pattern
                });
            }
            catch (Exception)
            {
                // Skip invalid patterns
            }
        }

        return compiled;
    }

    private string ConvertGlobToRegex(string globPattern)
    {
        // Escape special regex characters except * and ?
        var pattern = Regex.Escape(globPattern);

        // Convert glob wildcards to regex
        pattern = pattern.Replace("\\*", ".*");
        pattern = pattern.Replace("\\?", ".");

        // Anchor the pattern
        return $"^{pattern}$";
    }

    private static string? NormalizeDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        // Remove leading/trailing slashes and normalize separators
        return directory.Trim().Trim('/', '\\').Replace('\\', '/');
    }

    private static string NormalizePath(string path)
    {
        // Get full path and normalize separators
        return Path.GetFullPath(path).Replace('\\', '/');
    }

    private sealed class CompiledPattern
    {
        public required string Type { get; init; }
        public required Regex Regex { get; init; }
        public required string OriginalPattern { get; init; }
    }
}