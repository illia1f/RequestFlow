using BenchmarkDotNet.Attributes;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures.ShortCircuit;

namespace RequestFlow.Benchmarks;

[BenchmarkScope(ScopeLifetime.RetainedScope)]
public class ShortCircuitRetainedScopeBenchmarks
{
    [Params(ShortCircuitOutcome.AuthorizationReject, ShortCircuitOutcome.CacheHit, ShortCircuitOutcome.FullExecution)]
    public ShortCircuitOutcome Outcome { get; set; }

    private RetainedScopeHost<IRequestDispatcher> _task = null!;
    private RetainedScopeHost<IValueRequestDispatcher> _value = null!;

    private ShortCircuitTaskRequest _taskRequest = null!;
    private ShortCircuitValueRequest _valueRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _task = new(ShortCircuitServices.Build(bufferBytes: 0, valueTask: false));
        _value = new(ShortCircuitServices.Build(bufferBytes: 0, valueTask: true));

        _taskRequest = new(Outcome);
        _valueRequest = new(Outcome);

        TaskPath().GetAwaiter().GetResult();
        ValueTaskPath().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public Task<int> TaskPath()
        => _task.Service.SendAsync(_taskRequest, CancellationToken.None);

    [Benchmark]
    public ValueTask<int> ValueTaskPath()
        => _value.Service.SendAsync(_valueRequest, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _task.Dispose();
        _value.Dispose();
    }
}
