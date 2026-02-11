using System.Text.RegularExpressions;
using n2jSoft.ModularGuard.CLI.Configuration;
using n2jSoft.ModularGuard.CLI.Models;
using n2jSoft.ModularGuard.CLI.Services;
using n2jSoft.ModularGuard.CLI.Validation;
using Spectre.Console;
using Spectre.Console.Cli;
using ValidationResult = n2jSoft.ModularGuard.CLI.Validation.ValidationResult;

namespace n2jSoft.ModularGuard.CLI.Commands;

public sealed class WatchCommand : Command<WatchCommand.Settings>
{
    private readonly object _validationLock = new();

    private volatile bool _shouldExit;

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var absolutePath = Path.GetFullPath(settings.Path);

        if (!Directory.Exists(absolutePath))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Directory not found: {0}", absolutePath);
            return 1;
        }

        // Load configuration
        var configLoader = new ConfigurationLoader();
        var configuration = configLoader.TryLoadConfiguration(absolutePath, settings.Profile)
                            ?? configLoader.CreateDefaultConfiguration();

        if (!settings.Quiet)
        {
            var configSource = configLoader.TryLoadConfiguration(absolutePath) != null
                ? "Found configuration file"
                : "Using default configuration";

            if (!string.IsNullOrWhiteSpace(settings.Profile))
            {
                configSource += $" (profile: {settings.Profile})";
            }

            AnsiConsole.MarkupLine("[dim]{0}[/]", configSource);
        }

        // Initial validation
        if (!settings.Quiet)
        {
            AnsiConsole.MarkupLine("[cyan]Starting watch mode...[/]");
            AnsiConsole.MarkupLine("[dim]Press Ctrl+C to exit[/]");
            AnsiConsole.WriteLine();
        }

        RunValidation(absolutePath, configuration, settings);

        // Set up file system watcher
        using var watcher = new FileSystemWatcher(absolutePath)
        {
            Filter = "*.csproj",
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
        };

        watcher.Changed += (sender, e) => OnProjectFileChanged(e.FullPath, absolutePath, configuration, settings);
        watcher.Created += (sender, e) => OnProjectFileChanged(e.FullPath, absolutePath, configuration, settings);
        watcher.Deleted += (sender, e) => OnProjectFileDeleted(e.FullPath, settings);
        watcher.Renamed += (sender, e) =>
            OnProjectFileRenamed(e.OldFullPath, e.FullPath, absolutePath, configuration, settings);

        watcher.EnableRaisingEvents = true;

        if (!settings.Quiet)
        {
            AnsiConsole.MarkupLine("[green]✓ Watching for .csproj changes...[/]");
            AnsiConsole.WriteLine();
        }

        // Set up console cancellation handler
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            _shouldExit = true;
        };

        // Wait until Ctrl+C is pressed
        while (!_shouldExit && !cancellationToken.IsCancellationRequested)
        {
            Thread.Sleep(100);
        }

        if (!settings.Quiet)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Watch mode stopped.[/]");
        }

        return 0;
    }

    private void OnProjectFileChanged(string filePath, string basePath, LinterConfiguration configuration,
        Settings settings)
    {
        lock (_validationLock)
        {
            if (!settings.Quiet)
            {
                var relativePath = Path.GetRelativePath(basePath, filePath);
                AnsiConsole.MarkupLine("[yellow]⟳ Changed:[/] {0}", relativePath);
            }

            // Small delay to allow file writes to complete
            Thread.Sleep(100);

            RunValidation(basePath, configuration, settings);
        }
    }

    private void OnProjectFileDeleted(string filePath, Settings settings)
    {
        if (!settings.Quiet)
        {
            AnsiConsole.MarkupLine("[red]✗ Deleted:[/] {0}", Path.GetFileName(filePath));
        }
    }

    private void OnProjectFileRenamed(string oldPath, string newPath, string basePath,
        LinterConfiguration configuration, Settings settings)
    {
        if (!settings.Quiet)
        {
            AnsiConsole.MarkupLine("[blue]↻ Renamed:[/] {0} → {1}",
                Path.GetFileName(oldPath),
                Path.GetFileName(newPath));
        }

        lock (_validationLock)
        {
            RunValidation(basePath, configuration, settings);
        }
    }

    private void RunValidation(string absolutePath, LinterConfiguration configuration, Settings settings)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");

            if (!settings.Quiet)
            {
                AnsiConsole.MarkupLine("[dim]── [{0}] Validating...[/]", timestamp);
            }

            var discoveryService = new ProjectDiscoveryService();
            var projectPaths = discoveryService.DiscoverProjects(absolutePath).ToList();

            if (projectPaths.Count == 0)
            {
                if (!settings.Quiet)
                {
                    AnsiConsole.MarkupLine("[yellow]No .csproj files found.[/]");
                }

                return;
            }

            var modules = new List<ModuleInfo>();
            var typeDetector = new ConfigurableProjectTypeDetector(configuration);

            using (var loaderService = new ProjectLoaderService())
            {
                foreach (var projectPath in projectPaths)
                {
                    try
                    {
                        var projectInfo = loaderService.LoadProject(projectPath);

                        // Skip ignored projects
                        if (IsProjectIgnored(projectInfo.Name, configuration.IgnoredProjects))
                        {
                            continue;
                        }

                        var projectType = typeDetector.DetectProjectType(projectInfo.Name, projectPath, absolutePath);
                        var moduleName = typeDetector.ExtractModuleName(projectInfo.Name, projectType);

                        modules.Add(new ModuleInfo(moduleName, projectType, projectInfo));
                    }
                    catch (Exception ex)
                    {
                        if (settings.Verbose)
                        {
                            AnsiConsole.MarkupLine("[red]Failed to load {0}:[/] {1}",
                                Path.GetFileName(projectPath),
                                ex.Message);
                        }
                    }
                }
            }

            var validationEngine = new ConfigurableValidationEngine(configuration, settings.Verbose);
            var validationResult = validationEngine.ValidateAllWithResult(modules);

            if (validationResult.Violations.Count == 0)
            {
                if (!settings.Quiet)
                {
                    AnsiConsole.MarkupLine("[green]✓ No violations found[/] ({0} projects)", projectPaths.Count);
                    AnsiConsole.WriteLine();
                }
            }
            else
            {
                if (!settings.Quiet)
                {
                    AnsiConsole.MarkupLine("[red]✗ Found {0} violation(s)[/]", validationResult.Violations.Count);
                }

                DisplayViolations(validationResult, settings.Verbose);

                if (!settings.Quiet)
                {
                    AnsiConsole.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Validation error:[/] {0}", ex.Message);
            if (settings.Verbose)
            {
                AnsiConsole.WriteException(ex);
            }
        }
    }

    private static void DisplayViolations(ValidationResult validationResult, bool verbose)
    {
        foreach (var violation in validationResult.Violations.OrderBy(v => v.ProjectName).ThenBy(v => v.Severity))
        {
            var severityIcon = violation.Severity switch
            {
                ViolationSeverity.Error => "[red]⚠️[/]",
                ViolationSeverity.Warning => "[yellow]⚡[/]",
                ViolationSeverity.Info => "[blue]ℹ️[/]",
                _ => ""
            };

            if (verbose)
            {
                AnsiConsole.MarkupLine("  {0} [cyan]{1}[/] → [yellow]{2}[/]",
                    severityIcon,
                    violation.ProjectName,
                    violation.InvalidReference);
                AnsiConsole.MarkupLine("     [dim]{0}[/]", violation.Description);
            }
            else
            {
                AnsiConsole.MarkupLine("  {0} [cyan]{1}[/] → [yellow]{2}[/]",
                    severityIcon,
                    violation.ProjectName,
                    violation.InvalidReference);
            }
        }
    }

    private static bool IsProjectIgnored(string projectName, List<string> ignoredPatterns)
    {
        foreach (var pattern in ignoredPatterns)
        {
            if (MatchesGlobPattern(projectName, pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesGlobPattern(string projectName, string pattern)
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

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<PATH>")] public string Path { get; init; } = string.Empty;

        [CommandOption("-v|--verbose")] public bool Verbose { get; init; }

        [CommandOption("-q|--quiet")] public bool Quiet { get; init; }

        [CommandOption("-p|--profile")] public string? Profile { get; init; }
    }
}