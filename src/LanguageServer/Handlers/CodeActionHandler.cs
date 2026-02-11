using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace n2jSoft.ModularGuard.LanguageServer.Handlers;

/// <summary>
///     Provides code actions (quick fixes) for violations detected in .csproj files
/// </summary>
public sealed class CodeActionHandler : CodeActionHandlerBase
{
    private readonly ILogger<CodeActionHandler> _logger;

    public CodeActionHandler(ILogger<CodeActionHandler> logger)
    {
        _logger = logger;
    }

    protected override CodeActionRegistrationOptions CreateRegistrationOptions(
        CodeActionCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new CodeActionRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.csproj"),
            CodeActionKinds = new Container<CodeActionKind>(
                CodeActionKind.QuickFix,
                CodeActionKind.SourceFixAll
            )
        };
    }

    public override Task<CommandOrCodeActionContainer?> Handle(
        CodeActionParams request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Code action requested for {Uri}", request.TextDocument.Uri);

        // TODO: Implement actual code actions
        // This is scaffolding that compiles and can be extended later

        var codeActions = new List<CommandOrCodeAction>();
        return Task.FromResult<CommandOrCodeActionContainer?>(new CommandOrCodeActionContainer(codeActions));
    }

    public override Task<CodeAction> Handle(CodeAction request, CancellationToken cancellationToken)
    {
        return Task.FromResult(request);
    }
}