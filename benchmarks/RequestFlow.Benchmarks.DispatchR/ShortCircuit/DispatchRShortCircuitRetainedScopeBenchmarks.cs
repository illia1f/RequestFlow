using BenchmarkDotNet.Attributes;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures.ShortCircuit;

namespace RequestFlow.Benchmarks.DispatchR;

[BenchmarkScope(ScopeLifetime.RetainedScope)]
public class DispatchRShortCircuitRetainedScopeBenchmarks
{
    [Params(ShortCircuitOutcome.AuthorizationReject, ShortCircuitOutcome.CacheHit, ShortCircuitOutcome.FullExecution)]
    public ShortCircuitOutcome Outcome { get; set; }

    private RetainedScopeHost<global::RequestFlow.IRequestDispatcher> _requestFlow = null!;
    private RetainedScopeHost<global::DispatchR.IMediator> _dispatchR = null!;

    private ShortCircuitTaskRequest _requestFlowRequest = null!;
    private DispatchRShortCircuitRequest _dispatchRRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlow = new(ShortCircuitServices.Build(bufferBytes: 0, valueTask: false));
        _dispatchR = new(DispatchRShortCircuitSetup.Build(bufferBytes: 0));

        _requestFlowRequest = new(Outcome);
        _dispatchRRequest = new(Outcome);

        RequestFlow().GetAwaiter().GetResult();
        DispatchR().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public Task<int> RequestFlow()
        => _requestFlow.Service.SendAsync(_requestFlowRequest, CancellationToken.None);

    [Benchmark]
    public Task<int> DispatchR()
        => _dispatchR.Service.Send(_dispatchRRequest, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _dispatchR.Dispose();
    }
}
