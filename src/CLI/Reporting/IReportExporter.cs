using n2jSoft.ModularGuard.CLI.Models;
using n2jSoft.ModularGuard.CLI.Validation;

namespace n2jSoft.ModularGuard.CLI.Reporting;

public interface IReportExporter
{
    string Export(IReadOnlyList<ModuleInfo> modules, ValidationResult validationResult);
}