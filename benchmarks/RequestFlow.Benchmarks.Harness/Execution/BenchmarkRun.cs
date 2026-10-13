using BenchmarkDotNet.Configs;
using BenchmarkDotNet.ConsoleArguments;
using BenchmarkDotNet.ConsoleArguments.ListBenchmarks;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;

namespace RequestFlow.Benchmarks.Harness;

public sealed record BenchmarkRun(
    string Suite,
    BenchmarkTier Tier,
    IConfig Config,
    Job[] Jobs,
    string[] Arguments,
    string[] OriginalArguments,
    bool IsInformational)
{
    private static readonly string[] TierSwitches = ["--smoke", "--quick"];
    private static readonly string[] InformationalSwitches = ["--help", "-h", "--info", "--version"];
    private static readonly string[] PathOptions =
    [
        "--artifacts", "-a", "--cli", "--packages", "--coreRun", "--monoPath", "--ilcPackages",
        "--wasmEngine", "--customRuntimePack", "--AOTCompilerPath", "--wasmDataDir",
    ];

    public static BenchmarkRun Create(string suiteName, string[] args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suiteName);
        ArgumentNullException.ThrowIfNull(args);

        if (args.Any(argument => argument.StartsWith('@')))
            throw new ArgumentException("Response files (@file) are not supported. Pass the options directly.", nameof(args));

        BenchmarkTier tier = SelectTier(args);
        string[] arguments = args.Where(argument => !TierSwitches.Contains(argument, StringComparer.OrdinalIgnoreCase)).ToArray();

        MakePathsAbsolute(arguments);

        string artifactsPath = Path.GetFullPath(Path.Combine(
            "BenchmarkDotNet.Artifacts",
            suiteName,
            tier.ToString().ToLowerInvariant(),
            $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"));

        IConfig config = BenchmarkConfig.Create(tier, artifactsPath);
        var parserLog = new AccumulationLogger();
        var parsed = ConfigParser.Parse(arguments, parserLog, config);
        bool help = arguments.Length == 1 && InformationalSwitches.Contains(arguments[0], StringComparer.OrdinalIgnoreCase);

        if (!parsed.isSuccess && !help)
            throw new ArgumentException(
                $"BenchmarkDotNet rejected the command arguments.{Environment.NewLine}{ParserErrors(parserLog.GetLog())}", nameof(args));

        if (parsed.isSuccess && parsed.options.ArtifactsDirectory is { } artifacts)
            config = BenchmarkConfig.Create(tier, artifacts.FullName);

        bool listing = parsed.isSuccess && parsed.options.ListBenchmarkCaseMode != ListBenchmarkCaseMode.Disabled;
        Job[] jobs = parsed.isSuccess ? parsed.config.GetJobs().ToArray() : [];

        return new BenchmarkRun(suiteName, tier, config, jobs, arguments, args.ToArray(), help || listing);
    }

    // Keeps the parser errors and drops the generic usage examples that follow them.
    private static string ParserErrors(string log)
    {
        int usage = log.IndexOf("USAGE:", StringComparison.Ordinal);

        return (usage < 0 ? log : log[..usage]).Trim();
    }

    // The runner changes to the suite folder before BenchmarkDotNet parses the arguments again.
    private static void MakePathsAbsolute(string[] arguments)
    {
        for (int index = 0; index < arguments.Length; index++)
        {
            string argument = arguments[index];
            int equals = argument.IndexOf('=');
            string option = equals < 0 ? argument : argument[..equals];

            if (!PathOptions.Contains(option, StringComparer.OrdinalIgnoreCase))
            {
                if (argument.StartsWith("-a", StringComparison.Ordinal) && argument.Length > 2)
                    arguments[index] = "-a" + Path.GetFullPath(argument[2..]);

                continue;
            }

            if (equals >= 0)
                arguments[index] = argument[..(equals + 1)] + Path.GetFullPath(argument[(equals + 1)..]);
            else if (index + 1 < arguments.Length && !arguments[index + 1].StartsWith('-'))
                arguments[++index] = Path.GetFullPath(arguments[index]);

            if (option.Equals("--coreRun", StringComparison.OrdinalIgnoreCase))
                while (index + 1 < arguments.Length && !arguments[index + 1].StartsWith('-'))
                    arguments[++index] = Path.GetFullPath(arguments[index]);
        }
    }

    private static BenchmarkTier SelectTier(string[] args)
    {
        bool smoke = args.Contains("--smoke", StringComparer.OrdinalIgnoreCase);
        bool quick = args.Contains("--quick", StringComparer.OrdinalIgnoreCase);

        if (smoke && quick)
            throw new ArgumentException("Use either --smoke or --quick, not both.", nameof(args));

        return smoke ? BenchmarkTier.Smoke : quick ? BenchmarkTier.Quick : BenchmarkTier.Full;
    }
}
