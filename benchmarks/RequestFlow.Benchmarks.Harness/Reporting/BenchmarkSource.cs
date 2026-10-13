using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Serialization;

namespace RequestFlow.Benchmarks.Harness;

internal sealed record BenchmarkSource(string? Commit, bool IsDirty, string[]? Changes)
{
    private static readonly string[] BuildFiles =
    [
        "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json", "nuget.config",
        "RequestFlow.snk",
    ];

    internal static readonly BenchmarkSource Unidentified = new(null, false, null);

    [JsonIgnore]
    public bool IsIdentified => Commit is not null && Changes is not null;

    internal bool IsSameAs(BenchmarkSource other)
        => IsIdentified && other.IsIdentified && Commit == other.Commit && Changes!.SequenceEqual(other.Changes!);

    /// <summary>
    /// Reads HEAD and the Git status of every path that feeds the suite build, ignored files included.
    /// </summary>
    internal static BenchmarkSource Read(Assembly assembly, string artifactsPath)
    {
        string? projectDirectory = BenchmarkProjectDirectory.Read(assembly);
        string? root = projectDirectory is null ? null : RunGit(projectDirectory, "rev-parse", "--show-toplevel");

        if (root is null)
            return Unidentified;

        root = Path.GetFullPath(root);

        List<string> arguments =
        [
            "status", "--porcelain", "--untracked-files=all", "--ignored", "--",
            .. InputPaths(assembly, root),
        ];

        if (Relative(root, artifactsPath) is var artifacts && IsInside(artifacts))
            arguments.Add(":(exclude)" + artifacts);

        string? commit = RunGit(root, "rev-parse", "HEAD");
        string? status = RunGit(root, [.. arguments]);

        if (commit is null || status is null)
            return Unidentified;

        string[] changes = ReadChanges(status);

        return new BenchmarkSource(commit.Trim(), changes.Length > 0, changes);
    }

    /// <summary>
    /// Reads the commit that the SDK appends after <c>+</c> in the informational version at build time.
    /// </summary>
    internal static string? ReadBuildCommit(Assembly assembly)
    {
        string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        int separator = version?.LastIndexOf('+') ?? -1;

        return separator < 0 ? null : version![(separator + 1)..];
    }

    /// <summary>
    /// Splits porcelain status output into lines and drops build output below <c>bin</c> or <c>obj</c> folders.
    /// </summary>
    /// <remarks>
    /// A <c>:(exclude,glob)</c> pathspec would make Git also report nested repositories outside the input paths.
    /// </remarks>
    internal static string[] ReadChanges(string status)
        => status.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !IsBuildOutput(line))
            .ToArray();

    // A line is two status letters, a space and a path, or for a rename the old and new paths joined by " -> ".
    private static bool IsBuildOutput(string line)
        => line.Length > 3 && line[3..].Split(" -> ").All(path => path.Trim('"').Split('/').Any(segment => segment is "bin" or "obj"));

    private static SortedSet<string> InputPaths(Assembly assembly, string root)
    {
        var paths = new SortedSet<string>(StringComparer.Ordinal) { "src" };

        foreach (string project in ProjectDirectories(assembly))
        {
            paths.Add(Relative(root, project));

            for (string? directory = Path.GetDirectoryName(project);
                directory is not null && IsInside(Relative(root, directory));
                directory = Path.GetDirectoryName(directory))
            {
                foreach (string file in BuildFiles.Select(name => Path.Combine(directory, name)).Where(File.Exists))
                    paths.Add(Relative(root, file));
            }
        }

        return paths;
    }

    private static IEnumerable<string> ProjectDirectories(Assembly assembly)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<Assembly>([assembly]);

        while (pending.TryDequeue(out Assembly? current))
        {
            if (!visited.Add(current.FullName!) || BenchmarkProjectDirectory.Read(current) is not { } directory)
                continue;

            yield return directory;

            foreach (AssemblyName reference in current.GetReferencedAssemblies())
            {
                if (TryLoad(reference) is { } referenced)
                    pending.Enqueue(referenced);
            }
        }
    }

    private static Assembly? TryLoad(AssemblyName name)
    {
        try
        {
            return Assembly.Load(name);
        }
        catch
        {
            return null;
        }
    }

    private static string Relative(string root, string path)
        => Path.GetRelativePath(root, Path.GetFullPath(path)).Replace('\\', '/');

    private static bool IsInside(string relative)
        => relative != ".." && !relative.StartsWith("../", StringComparison.Ordinal) && !Path.IsPathRooted(relative);

    private static string? RunGit(string workingDirectory, params string[] arguments)
    {
        try
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (string argument in arguments)
                start.ArgumentList.Add(argument);

            using Process? process = Process.Start(start);

            if (process is null)
                return null;

            string output = process.StandardOutput.ReadToEnd();

            process.WaitForExit();

            return process.ExitCode == 0 ? output.TrimEnd() : null;
        }
        catch
        {
            return null;
        }
    }
}
