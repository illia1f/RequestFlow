using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures.ShortCircuit;

namespace RequestFlow.Benchmarks;

[BenchmarkScope(ScopeLifetime.ScopePerOperation)]
public class ShortCircuitScopePerOperationBenchmarks
{
    [Params(ShortCircuitOutcome.AuthorizationReject, ShortCircuitOutcome.CacheHit, ShortCircuitOutcome.FullExecution)]
    public ShortCircuitOutcome Outcome { get; set; }

    [Params(0, 4096)]
    public int BufferBytes { get; set; }

    private ScopePerOperationHost _task = null!;
    private ScopePerOperationHost _value = null!;

    private ShortCircuitTaskRequest _taskRequest = null!;
    private ShortCircuitValueRequest _valueRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _task = new(ShortCircuitServices.Build(BufferBytes, valueTask: false));
        _value = new(ShortCircuitServices.Build(BufferBytes, valueTask: true));

        _taskRequest = new(Outcome);
        _valueRequest = new(Outcome);

        TaskPath().GetAwaiter().GetResult();
        ValueTaskPath().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public async Task<int> TaskPath()
    {
        using IServiceScope scope = _task.Provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        return await dispatcher.SendAsync(_taskRequest, CancellationToken.None).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task<int> ValueTaskPath()
    {
        using IServiceScope scope = _value.Provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IValueRequestDispatcher>();

        return await dispatcher.SendAsync(_valueRequest, CancellationToken.None).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _task.Dispose();
        _value.Dispose();
    }
}
