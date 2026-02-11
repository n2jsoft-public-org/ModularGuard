using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using n2jSoft.ModularGuard.LanguageServer.Handlers;
using OmniSharp.Extensions.LanguageServer.Server;

namespace n2jSoft.ModularGuard.LanguageServer;

/// <summary>
///     Entry point for the Modular Monolith Linter Language Server
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        var server = await OmniSharp.Extensions.LanguageServer.Server.LanguageServer.From(options =>
            options
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
                .ConfigureLogging(x => x
                    .AddLanguageProtocolLogging()
                    .SetMinimumLevel(LogLevel.Debug))
                .WithServices(ConfigureServices)
                .WithHandler<DiagnosticHandler>()
                .WithHandler<CodeActionHandler>()
        );

        await server.WaitForExit;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Register any additional services needed by the language server
    }
}