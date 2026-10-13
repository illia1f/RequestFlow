using BenchmarkDotNet.Attributes;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks.MediatR;

[BenchmarkScope(ScopeLifetime.ScopePerOperation)]
public class MediatRStreamScopePerOperationBenchmarks
{
    [Params(1, 10, 100)]
    public int ItemCount { get; set; }

    [Params(CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    [Params(0, 1, 5)]
    public int StageCount { get; set; }

    private ScopePerOperationHost _requestFlow = null!;
    private ScopePerOperationHost _mediatR = null!;

    private StreamRequest _requestFlowRequest = null!;
    private MediatRStreamRequest _mediatRRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlow = new(RequestFlowSetup.Build(RequestFlowSetup.StreamServices(StageCount, MediatRSetup.DispatcherLifetime)));
        _mediatR = new(MediatRSetup.Build(MediatRSetup.StreamServices(StageCount)));

        _requestFlowRequest = new StreamRequest(ItemCount, Completion);
        _mediatRRequest = new MediatRStreamRequest(ItemCount, Completion);

        RequestFlow().GetAwaiter().GetResult();
        MediatR().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public async Task<int> RequestFlow()
    {
        using IServiceScope scope = _requestFlow.Provider.CreateScope();

        var streamDispatcher = scope.ServiceProvider
            .GetRequiredService<IStreamDispatcher>();

        var stream = streamDispatcher.Stream(_requestFlowRequest, CancellationToken.None);

        return await BenchmarkWorkload.Consume(stream).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task<int> MediatR()
    {
        using IServiceScope scope = _mediatR.Provider.CreateScope();

        var dispatcher = scope.ServiceProvider
            .GetRequiredService<ISender>();

        var stream = dispatcher.CreateStream(_mediatRRequest, CancellationToken.None);

        return await BenchmarkWorkload.Consume(stream).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _mediatR.Dispose();
    }
}
