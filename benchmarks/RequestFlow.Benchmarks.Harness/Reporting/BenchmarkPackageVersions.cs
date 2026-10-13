using System.Text.Json;

namespace RequestFlow.Benchmarks.Harness;

internal static class BenchmarkPackageVersions
{
    internal static IReadOnlyDictionary<string, string> Read(System.Reflection.Assembly assembly)
    {
        var packages = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string depsPath = Path.ChangeExtension(assembly.Location, ".deps.json");

        if (File.Exists(depsPath))
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(depsPath));

            AddPackages(document, packages);
        }

        using Stream? assets = assembly.GetManifestResourceStream("RequestFlow.Benchmarks.BuildDependencies.json");

        if (assets is not null)
        {
            using JsonDocument document = JsonDocument.Parse(assets);

            AddPackages(document, packages);
        }

        return packages;
    }

    private static void AddPackages(JsonDocument document, IDictionary<string, string> packages)
    {
        if (!document.RootElement.TryGetProperty("libraries", out JsonElement libraries))
            return;

        foreach (JsonProperty library in libraries.EnumerateObject())
        {
            if (!library.Value.TryGetProperty("type", out JsonElement type) ||
                !type.GetString()!.Equals("package", StringComparison.OrdinalIgnoreCase))
                continue;

            int separator = library.Name.LastIndexOf('/');

            if (separator > 0)
                packages[library.Name[..separator]] = library.Name[(separator + 1)..];
        }
    }
}
