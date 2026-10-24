using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures.ShortCircuit;

namespace RequestFlow.Benchmarks.DispatchR;

[BenchmarkScope(ScopeLifetime.ScopePerOperation)]
public class DispatchRShortCircuitScopePerOperationBenchmarks
{
    [Params(ShortCircuitOutcome.AuthorizationReject, ShortCircuitOutcome.CacheHit, ShortCircuitOutcome.FullExecution)]
    public ShortCircuitOutcome Outcome { get; set; }

    [Params(0, 4096)]
    public int BufferBytes { get; set; }

    private ScopePerOperationHost _requestFlow = null!;
    private ScopePerOperationHost _dispatchR = null!;

    private ShortCircuitTaskRequest _requestFlowRequest = null!;
    private DispatchRShortCircuitRequest _dispatchRRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlow = new(ShortCircuitServices.Build(BufferBytes, valueTask: false));
        _dispatchR = new(DispatchRShortCircuitSetup.Build(BufferBytes));

        _requestFlowRequest = new(Outcome);
        _dispatchRRequest = new(Outcome);

        RequestFlow().GetAwaiter().GetResult();
        DispatchR().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public async Task<int> RequestFlow()
    {
        using IServiceScope scope = _requestFlow.Provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<global::RequestFlow.IRequestDispatcher>();

        return await dispatcher.SendAsync(_requestFlowRequest, CancellationToken.None).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task<int> DispatchR()
    {
        using IServiceScope scope = _dispatchR.Provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<global::DispatchR.IMediator>();

        return await dispatcher.Send(_dispatchRRequest, CancellationToken.None).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _dispatchR.Dispose();
    }
}
