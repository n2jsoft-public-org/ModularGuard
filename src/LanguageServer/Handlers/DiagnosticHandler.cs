using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace n2jSoft.ModularGuard.LanguageServer.Handlers;

/// <summary>
///     Handles diagnostic requests for .csproj files
/// </summary>
public sealed class DiagnosticHandler : TextDocumentSyncHandlerBase
{
    private readonly Dictionary<DocumentUri, List<Diagnostic>> _diagnosticCache = new();
    private readonly ILanguageServerFacade _languageServer;
    private readonly ILogger<DiagnosticHandler> _logger;

    public DiagnosticHandler(ILogger<DiagnosticHandler> logger, ILanguageServerFacade languageServer)
    {
        _logger = logger;
        _languageServer = languageServer;
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new TextDocumentAttributes(uri, "xml");
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new TextDocumentSyncRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.csproj")
        };
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Document opened: {Uri}", request.TextDocument.Uri);
        return AnalyzeDocumentAsync(request.TextDocument.Uri, cancellationToken);
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Document changed: {Uri}", request.TextDocument.Uri);
        return AnalyzeDocumentAsync(request.TextDocument.Uri, cancellationToken);
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Document saved: {Uri}", request.TextDocument.Uri);
        return AnalyzeDocumentAsync(request.TextDocument.Uri, cancellationToken);
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Document closed: {Uri}", request.TextDocument.Uri);

        // Clear diagnostics when document is closed
        _diagnosticCache.Remove(request.TextDocument.Uri);
        _languageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = request.TextDocument.Uri,
            Diagnostics = new Container<Diagnostic>()
        });

        return Unit.Task;
    }

    private async Task<Unit> AnalyzeDocumentAsync(DocumentUri uri, CancellationToken cancellationToken)
    {
        // TODO: Implement actual validation logic
        // This is scaffolding that compiles and can be extended later

        _logger.LogInformation("Analysis not yet implemented for {Uri}", uri);

        return Unit.Value;
    }
}