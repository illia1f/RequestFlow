using System.Reflection;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace RequestFlow.Benchmarks.Harness;

public static class SuiteRunner
{
    public static int Run(
        Assembly assembly,
        string suiteName,
        string[] args,
        Func<BenchmarkProbeResult> probe,
        Action? reportLifetimes = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(suiteName);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(probe);

        if (IsCommand(args, "probe"))
            return RunCommand("Probe", () => RunProbe(probe));

        if (IsCommand(args, "lifetimes"))
        {
            if (reportLifetimes is null)
                return Fail("This suite does not report lifetimes.");

            return RunCommand("Lifetime report", reportLifetimes);
        }

        if (args.Any(argument => argument.Equals("probe", StringComparison.OrdinalIgnoreCase) ||
                                 argument.Equals("lifetimes", StringComparison.OrdinalIgnoreCase)))
            return Fail("Use probe or lifetimes as the only command argument.");

        try
        {
            BenchmarkRun run = BenchmarkRun.Create(suiteName, args);

            if (run.IsInformational)
            {
                BenchmarkSwitcher.FromAssembly(assembly).Run(run.Arguments, run.Config);

                return 0;
            }

            return RunBenchmarks(assembly, run, probe);
        }
        catch (Exception exception)
        {
            return Fail($"Benchmark run failed: {Describe(exception)}");
        }
    }

    private static int RunCommand(string name, Action command)
    {
        try
        {
            command();

            return 0;
        }
        catch (Exception exception)
        {
            return Fail($"{name} failed: {Describe(exception)}");
        }
    }

    private static string Describe(Exception exception)
        => exception.InnerException is { } inner ? exception.Message + " " + Describe(inner) : exception.Message;

    private static BenchmarkProbeResult RunProbe(Func<BenchmarkProbeResult> probe)
    {
        BenchmarkProbeResult result = probe();

        Console.WriteLine(
            $"Probe passed: checked {result.Classes} benchmark classes, {result.ParameterSets} parameter sets and {result.Cases} cases.");

        return result;
    }

    // Writes run-manifest.json before and after BenchmarkDotNet, so an interrupted run stays at status running.
    // A full run probes first and fails without timing anything when the suite build is not from HEAD or the probe throws.
    private static int RunBenchmarks(Assembly assembly, BenchmarkRun run, Func<BenchmarkProbeResult> probe)
    {
        string artifactsPath = Path.GetFullPath(run.Config.ArtifactsPath);
        string manifestPath = Path.Combine(artifactsPath, "run-manifest.json");

        if (Directory.Exists(artifactsPath) && Directory.EnumerateFileSystemEntries(artifactsPath).Any())
            return Fail($"Artifacts path '{artifactsPath}' is not empty. Choose an unused directory.");

        Directory.CreateDirectory(artifactsPath);

        BenchmarkRunManifest manifest = BenchmarkRunManifest.Start(
            run,
            BenchmarkSource.Read(assembly, artifactsPath),
            BenchmarkHostEnvironment.Read(),
            BenchmarkPackageVersions.Read(assembly),
            run.Jobs.Select(BenchmarkJobDescription.Create).ToArray());

        manifest.Write(manifestPath);

        if (run.Tier == BenchmarkTier.Full)
        {
            // The probe runs the loaded build, but BenchmarkDotNet rebuilds the suite from the working tree.
            string? buildCommit = BenchmarkSource.ReadBuildCommit(assembly);

            if (manifest.Source.IsIdentified &&
                !string.Equals(buildCommit, manifest.Source.Commit, StringComparison.OrdinalIgnoreCase))
            {
                return Finish(
                    BenchmarkRunStatus.Failed(
                        $"The suite was built from commit {buildCommit ?? "unknown"}, but HEAD is {manifest.Source.Commit}. Rebuild the suite before a full run."),
                    manifest.EffectiveJobs,
                    []);
            }

            BenchmarkProbeResult probeResult;

            try
            {
                probeResult = RunProbe(probe);
            }
            catch (Exception exception)
            {
                return Finish(BenchmarkRunStatus.Failed($"Probe failed: {Describe(exception)}"), manifest.EffectiveJobs, []);
            }

            manifest = manifest.WithProbe(probeResult);
            manifest.Write(manifestPath);
        }

        // BenchmarkDotNet searches for the suite's project below the nearest solution above the working directory.
        // Starting in the suite folder makes that the solution nearest the suite, whatever directory the run started in.
        if (BenchmarkProjectDirectory.Read(assembly) is { } projectDirectory)
            Directory.SetCurrentDirectory(projectDirectory);

        BenchmarkRunStatus status;
        BenchmarkJobDescription[] jobs = manifest.EffectiveJobs;
        string[] warnings = [];

        try
        {
            Summary[] summaries = BenchmarkSwitcher.FromAssembly(assembly).Run(run.Arguments, run.Config).ToArray();
            BenchmarkJobDescription[] executedJobs = DescribeJobs(
                summaries.SelectMany(summary => summary.Reports).Select(report => report.BenchmarkCase));

            if (executedJobs.Length != 0)
                jobs = executedJobs;

            status = BenchmarkRunStatus.Determine(summaries);
            warnings = CollectWarnings(summaries, run.Tier);
        }
        catch (Exception exception)
        {
            status = BenchmarkRunStatus.Failed(Describe(exception));
        }

        return Finish(status, jobs, warnings);

        int Finish(BenchmarkRunStatus status, BenchmarkJobDescription[] jobs, string[] warnings)
        {
            manifest
                .Finish(
                    status,
                    jobs,
                    warnings,
                    BenchmarkSource.Read(assembly, artifactsPath),
                    BenchmarkArgumentPolicy.HasMeasurementOverrides(run.Arguments))
                .Write(manifestPath);

            return status == BenchmarkRunStatus.Completed ? 0 : Fail(status.Error!);
        }
    }

    private static string[] CollectWarnings(Summary[] summaries, BenchmarkTier tier)
    {
        IEnumerable<string> validationWarnings = summaries.SelectMany(summary => summary.ValidationErrors)
            .Where(error => !error.IsCritical)
            .Select(error => error.Message);
        IEnumerable<string> accuracyWarnings = tier == BenchmarkTier.Full
            ? summaries.SelectMany(AccuracyWarnings)
            : [];

        return validationWarnings
            .Concat(summaries.SelectMany(AnalysisWarnings))
            .Concat(accuracyWarnings)
            .Distinct()
            .ToArray();
    }

    private static IEnumerable<string> AccuracyWarnings(Summary summary)
        => summary.Reports
            .Where(report => report.ResultStatistics is not null)
            .Select(report => BenchmarkAccuracyWarnings.Describe(
                report.BenchmarkCase.DisplayInfo,
                report.ResultStatistics!.Mean,
                report.ResultStatistics.ConfidenceInterval.Margin))
            .OfType<string>();

    private static IEnumerable<string> AnalysisWarnings(Summary summary)
    {
        ImmutableConfig[] configs = summary.BenchmarksCases.Select(benchmark => benchmark.Config).Distinct().ToArray();
        var analyserConclusions = configs.SelectMany(config => config.GetAnalysers()).Distinct()
            .SelectMany(analyser => analyser.Analyse(summary));

        var configurationConclusions = configs.SelectMany(config => config.ConfigAnalysisConclusion);

        return analyserConclusions.Concat(configurationConclusions)
            .Distinct()
            .Select(conclusion => $"{conclusion.AnalyserId} ({conclusion.Kind}), " +
                $"{conclusion.Report?.BenchmarkCase.DisplayInfo ?? summary.Title}: {conclusion.Message}");
    }

    private static BenchmarkJobDescription[] DescribeJobs(IEnumerable<BenchmarkCase> cases)
        => cases.Select(benchmark => (Job: BenchmarkJobDescription.Create(benchmark.Job), Benchmark: benchmark.DisplayInfo))
            .GroupBy(item => (item.Job.Id, item.Job.Description))
            .Select(group => group.First().Job with
            {
                Benchmarks = group.Select(item => item.Benchmark).Distinct().ToArray(),
            })
            .ToArray();

    private static bool IsCommand(string[] args, string command)
        => args.Length == 1 && args[0].Equals(command, StringComparison.OrdinalIgnoreCase);

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);

        return 1;
    }
}
