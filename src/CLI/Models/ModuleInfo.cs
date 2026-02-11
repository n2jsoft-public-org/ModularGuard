namespace n2jSoft.ModularGuard.CLI.Models;

public sealed record ModuleInfo(
    string ModuleName,
    string Type,
    ProjectInfo ProjectInfo);