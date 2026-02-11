namespace n2jSoft.ModularGuard.CLI.Services;

public sealed class ProjectDiscoveryService
{
    public IEnumerable<string> DiscoverProjects(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return Enumerable.Empty<string>();
        }

        try
        {
            return Directory.EnumerateFiles(
                rootPath,
                "*.csproj",
                SearchOption.AllDirectories);
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }
}