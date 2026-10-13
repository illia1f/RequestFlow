using System.Reflection;

namespace RequestFlow.Benchmarks.Harness;

internal static class BenchmarkProjectDirectory
{
    internal static string? Read(Assembly assembly)
    {
        string? directory = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => attribute.Key == "RequestFlow.Benchmarks.ProjectDirectory")?.Value;

        return directory is not null && Path.IsPathFullyQualified(directory) ? directory : null;
    }
}
