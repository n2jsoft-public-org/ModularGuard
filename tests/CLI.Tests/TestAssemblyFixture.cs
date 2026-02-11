using Microsoft.Build.Locator;

namespace n2jSoft.ModularGuard.CLI.Tests;

public sealed class TestAssemblyFixture : IDisposable
{
    public TestAssemblyFixture()
    {
        // Initialize MSBuildLocator once for all tests
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}

[CollectionDefinition("Assembly")]
public class AssemblyCollection : ICollectionFixture<TestAssemblyFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}