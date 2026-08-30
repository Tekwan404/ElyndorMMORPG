using Elyndor.Core;

namespace Elyndor.UnitTests.Architecture;

public sealed class CoreDependencyTests
{
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Npgsql")]
    [InlineData("StackExchange.Redis")]
    public void CoreDoesNotReferenceInfrastructurePackages(string forbiddenAssemblyPrefix)
    {
        string[] referencedAssemblies = typeof(CoreAssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            referencedAssemblies,
            assemblyName => assemblyName.StartsWith(forbiddenAssemblyPrefix, StringComparison.Ordinal));
    }
}
