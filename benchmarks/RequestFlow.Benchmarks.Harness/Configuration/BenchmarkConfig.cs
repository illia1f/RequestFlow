using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Validators;
using Perfolizer.Horology;

namespace RequestFlow.Benchmarks.Harness;

public static class BenchmarkConfig
{
    internal const double MaximumRelativeError = 0.05;

    public static IConfig Create(BenchmarkTier tier, string artifactsPath)
    {
        Job job = tier switch
        {
            BenchmarkTier.Full => Job.Default
                .WithMinIterationTime(TimeInterval.FromMilliseconds(500))
                .WithLaunchCount(2)
                .WithMinWarmupCount(15)
                .WithMaxWarmupCount(50)
                .WithMinIterationCount(60)
                .WithMaxIterationCount(100)
                .WithMaxRelativeError(MaximumRelativeError),
            BenchmarkTier.Quick => Job.Default
                .WithIterationTime(TimeInterval.FromMilliseconds(250))
                .WithLaunchCount(1)
                .WithWarmupCount(3)
                .WithIterationCount(8),
            BenchmarkTier.Smoke => Job.Dry,
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown benchmark tier."),
        };

        ManualConfig config = ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(job.AsDefault())
            // With several --runtimes, BenchmarkDotNet makes the first runtime a baseline job; this keeps ratios within each runtime.
            .AddJob(Job.Default.WithBaseline(false).AsMutator())
            .AddValidator(JitOptimizationsValidator.FailOnError)
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddDiagnoser(ThreadingDiagnoser.Default)
            .AddExporter(MarkdownExporter.Default)
            .AddExporter(JsonExporter.Full)
            .WithOrderer(new DefaultOrderer(SummaryOrderPolicy.Declared));

        config.ArtifactsPath = artifactsPath;

        return config;
    }
}
