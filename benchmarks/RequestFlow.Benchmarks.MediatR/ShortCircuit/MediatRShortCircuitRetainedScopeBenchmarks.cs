using BenchmarkDotNet.Attributes;
using MediatR;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures.ShortCircuit;

namespace RequestFlow.Benchmarks.MediatR;

[BenchmarkScope(ScopeLifetime.RetainedScope)]
public class MediatRShortCircuitRetainedScopeBenchmarks
{
    [Params(ShortCircuitOutcome.AuthorizationReject, ShortCircuitOutcome.CacheHit, ShortCircuitOutcome.FullExecution)]
    public ShortCircuitOutcome Outcome { get; set; }

    private RetainedScopeHost<IRequestDispatcher> _requestFlow = null!;
    private RetainedScopeHost<ISender> _mediatR = null!;

    private ShortCircuitTaskRequest _requestFlowRequest = null!;
    private MediatRShortCircuitRequest _mediatRRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlow = new(ShortCircuitServices.Build(bufferBytes: 0, valueTask: false, dispatcherLifetime: MediatRSetup.DispatcherLifetime));
        _mediatR = new(MediatRShortCircuitSetup.Build(bufferBytes: 0));

        _requestFlowRequest = new(Outcome);
        _mediatRRequest = new(Outcome);

        RequestFlow().GetAwaiter().GetResult();
        MediatR().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public Task<int> RequestFlow()
        => _requestFlow.Service.SendAsync(_requestFlowRequest, CancellationToken.None);

    [Benchmark]
    public Task<int> MediatR()
        => _mediatR.Service.Send(_mediatRRequest, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _mediatR.Dispose();
    }
}
