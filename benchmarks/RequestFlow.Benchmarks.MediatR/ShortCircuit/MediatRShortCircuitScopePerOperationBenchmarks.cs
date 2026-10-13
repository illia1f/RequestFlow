using BenchmarkDotNet.Attributes;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures.ShortCircuit;

namespace RequestFlow.Benchmarks.MediatR;

[BenchmarkScope(ScopeLifetime.ScopePerOperation)]
public class MediatRShortCircuitScopePerOperationBenchmarks
{
    [Params(ShortCircuitOutcome.AuthorizationReject, ShortCircuitOutcome.CacheHit, ShortCircuitOutcome.FullExecution)]
    public ShortCircuitOutcome Outcome { get; set; }

    [Params(0, 4096)]
    public int BufferBytes { get; set; }

    private ScopePerOperationHost _requestFlow = null!;
    private ScopePerOperationHost _mediatR = null!;
    private ShortCircuitTaskRequest _requestFlowRequest = null!;
    private MediatRShortCircuitRequest _mediatRRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlow = new(ShortCircuitServices.Build(BufferBytes, valueTask: false, dispatcherLifetime: MediatRSetup.DispatcherLifetime));
        _mediatR = new(MediatRShortCircuitSetup.Build(BufferBytes));

        _requestFlowRequest = new(Outcome);
        _mediatRRequest = new(Outcome);

        RequestFlow().GetAwaiter().GetResult();
        MediatR().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public async Task<int> RequestFlow()
    {
        using IServiceScope scope = _requestFlow.Provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        return await dispatcher.SendAsync(_requestFlowRequest, CancellationToken.None).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task<int> MediatR()
    {
        using IServiceScope scope = _mediatR.Provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ISender>();

        return await dispatcher.Send(_mediatRRequest, CancellationToken.None).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _mediatR.Dispose();
    }
}
