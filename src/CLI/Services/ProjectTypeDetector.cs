namespace n2jSoft.ModularGuard.CLI.Services;

public sealed class ProjectTypeDetector
{
    public string DetectProjectType(string projectName)
    {
        // Shared projects
        if (projectName == "Shared.Core")
        {
            return "shared-core";
        }

        if (projectName == "Shared.Infrastructure")
        {
            return "shared-infrastructure";
        }

        if (projectName == "Shared.App.Admin")
        {
            return "shared-app-admin";
        }

        if (projectName == "Shared.App.Private")
        {
            return "shared-app-private";
        }

        if (projectName == "Shared.App.Public")
        {
            return "shared-app-public";
        }

        // Module projects - check suffix patterns
        if (projectName.EndsWith(".Core"))
        {
            return "core";
        }

        if (projectName.EndsWith(".Infrastructure"))
        {
            return "infrastructure";
        }

        if (projectName.EndsWith(".Admin.App"))
        {
            return "admin-app";
        }

        if (projectName.EndsWith(".Admin.Endpoints"))
        {
            return "admin-endpoints";
        }

        if (projectName.EndsWith(".Private.App"))
        {
            return "private-app";
        }

        if (projectName.EndsWith(".Private.Endpoints"))
        {
            return "private-endpoints";
        }

        if (projectName.EndsWith(".Public.App"))
        {
            return "public-app";
        }

        if (projectName.EndsWith(".Public.Endpoints"))
        {
            return "public-endpoints";
        }

        if (projectName.EndsWith(".Shared.Events"))
        {
            return "shared-events";
        }

        if (projectName.EndsWith(".Shared.Messages"))
        {
            return "shared-messages";
        }

        return "unknown";
    }

    public string ExtractModuleName(string projectName, string projectType)
    {
        if (projectType == "unknown")
        {
            return "Unknown";
        }

        // Shared projects belong to "Shared" module
        if (projectName.StartsWith("Shared"))
        {
            return "Shared";
        }

        // Extract module name by removing the suffix
        return projectType switch
        {
            "core" => projectName[..^5], // Remove ".Core"
            "infrastructure" => projectName[..^15], // Remove ".Infrastructure"
            "admin-app" => projectName[..^10], // Remove ".Admin.App"
            "admin-endpoints" => projectName[..^16], // Remove ".Admin.Endpoints"
            "private-app" => projectName[..^12], // Remove ".Private.App"
            "private-endpoints" => projectName[..^18], // Remove ".Private.Endpoints"
            "public-app" => projectName[..^11], // Remove ".Public.App"
            "public-endpoints" => projectName[..^17], // Remove ".Public.Endpoints"
            "shared-events" => projectName[..^14], // Remove ".Shared.Events"
            "shared-messages" => projectName[..^16], // Remove ".Shared.Messages"
            _ => projectName
        };
    }
}