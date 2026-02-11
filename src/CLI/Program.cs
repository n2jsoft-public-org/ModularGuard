using Microsoft.Build.Locator;
using n2jSoft.ModularGuard.CLI.Commands;
using Spectre.Console.Cli;

MSBuildLocator.RegisterDefaults();

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("modularguard");

    config.AddCommand<CheckCommand>("check")
        .WithDescription("Validate project references against modular monolith architecture rules")
        .WithExample("check", ".");

    config.AddCommand<OptimizeCommand>("optimize")
        .WithDescription("Find and optionally remove unnecessary project references")
        .WithExample("optimize", ".")
        .WithExample("optimize", ".", "--apply")
        .WithExample("optimize", ".", "--format", "json", "--output", "optimization-report.json");

    config.AddCommand<FixCommand>("fix")
        .WithDescription("Automatically fix violations by removing invalid project references")
        .WithExample("fix", ".")
        .WithExample("fix", ".", "--dry-run")
        .WithExample("fix", ".", "--interactive");

    config.AddCommand<WatchCommand>("watch")
        .WithDescription("Watch for .csproj file changes and re-validate in real-time")
        .WithExample("watch", ".")
        .WithExample("watch", ".", "--verbose");
});

return app.Run(args);