using BenchmarkDotNet.Attributes;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks;

/// <summary>
/// Compares synchronous Task and ValueTask void dispatch with matched scoped lifetimes.
/// </summary>
[BenchmarkScope(ScopeLifetime.RetainedScope)]
public class VoidCompletionBenchmarks
{
    /// <summary>
    /// Whether one forwarding stage wraps each handler.
    /// </summary>
    [Params(false, true)]
    public bool WithPipeline { get; set; }

    private RetainedScopeHost<IRequestDispatcher> _task = null!;
    private RetainedScopeHost<IValueRequestDispatcher> _value = null!;

    private readonly TaskVoidRequest _taskVoidRequest = new();
    private readonly ValueVoidRequest _valueVoidRequest = new();

    [GlobalSetup]
    public void Setup()
    {
        int stages = WithPipeline ? 1 : 0;

        _task = new(RequestFlowSetup.Build(RequestFlowSetup.TaskVoidRequestServices(stages)));
        _value = new(RequestFlowSetup.Build(RequestFlowSetup.ValueVoidRequestServices(stages)));

        TaskPath().GetAwaiter().GetResult();
        ValueTaskPath().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public Task TaskPath()
        => _task.Service.SendAsync(_taskVoidRequest, CancellationToken.None);

    [Benchmark]
    public ValueTask ValueTaskPath()
        => _value.Service.SendAsync(_valueVoidRequest, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _task.Dispose();
        _value.Dispose();
    }
}
