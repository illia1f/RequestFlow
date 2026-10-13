using System.Collections;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace RequestFlow.Benchmarks.Harness;

internal sealed record BenchmarkHostEnvironment(
    string OperatingSystem,
    string Runtime,
    string Architecture,
    int ProcessorCount,
    string? ProcessorAffinity,
    bool IsServerGc,
    IReadOnlyDictionary<string, string> RuntimeVariables)
{
    private static readonly string[] RuntimePrefixes = ["DOTNET_", "COMPlus_", "CORECLR_"];

    // SDK and host settings that leave code execution unchanged, as set by dotnet run, the CI workflow,
    // setup-dotnet and the GitHub runner images. A trailing * matches a prefix.
    private static readonly string[] IgnoredVariables =
    [
        "DOTNET_CLI_*",
        "DOTNET_LAUNCH_PROFILE",
        "DOTNET_NOLOGO",
        "DOTNET_ROOT*",
        "DOTNET_SKIP_FIRST_TIME_EXPERIENCE",
        "DOTNET_MULTILEVEL_LOOKUP",
    ];

    /// <summary>
    /// The captured runtime variables outside the ignore list, which disqualify a run from publication.
    /// </summary>
    internal string[] ExecutionVariables => RuntimeVariables.Keys.Where(name => !IsIgnored(name)).ToArray();

    internal static BenchmarkHostEnvironment Read() => Read(Environment.GetEnvironmentVariables());

    internal static BenchmarkHostEnvironment Read(IDictionary environment)
    {
        var variables = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (DictionaryEntry entry in environment)
        {
            if (entry.Key is string name && RuntimePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                variables[name] = entry.Value as string ?? "";
        }

        return new(
            RuntimeInformation.OSDescription,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.ProcessorCount,
            ReadProcessorAffinity(),
            GCSettings.IsServerGC,
            variables);
    }

    // BenchmarkDotNet child processes inherit this mask when no job sets an affinity.
    private static string? ReadProcessorAffinity()
    {
        if (!System.OperatingSystem.IsWindows() && !System.OperatingSystem.IsLinux())
            return null;

        using Process process = Process.GetCurrentProcess();

        return $"0x{process.ProcessorAffinity:X}";
    }

    private static bool IsIgnored(string name)
        => IgnoredVariables.Any(ignored => ignored.EndsWith('*')
            ? name.StartsWith(ignored[..^1], StringComparison.OrdinalIgnoreCase)
            : name.Equals(ignored, StringComparison.OrdinalIgnoreCase));
}
