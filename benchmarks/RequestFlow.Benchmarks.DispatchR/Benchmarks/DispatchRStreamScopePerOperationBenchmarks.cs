using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks.DispatchR;

[BenchmarkScope(ScopeLifetime.ScopePerOperation)]
public class DispatchRStreamScopePerOperationBenchmarks
{
    [Params(1, 10, 100)]
    public int ItemCount { get; set; }

    [Params(CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    [Params(0, 1, 5)]
    public int StageCount { get; set; }

    private ScopePerOperationHost _requestFlow = null!;
    private ScopePerOperationHost _dispatchR = null!;

    private StreamRequest _requestFlowRequest = null!;
    private DispatchRStreamRequest _dispatchRRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlow = new(RequestFlowSetup.Build(RequestFlowSetup.StreamServices(StageCount)));
        _dispatchR = new(DispatchRSetup.Build(DispatchRSetup.StreamServices(StageCount)));

        _requestFlowRequest = new StreamRequest(ItemCount, Completion);
        _dispatchRRequest = new DispatchRStreamRequest(ItemCount, Completion);

        RequestFlow().GetAwaiter().GetResult();
        DispatchR().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public async Task<int> RequestFlow()
    {
        using IServiceScope scope = _requestFlow.Provider.CreateScope();

        var streamDispatcher = scope.ServiceProvider
            .GetRequiredService<global::RequestFlow.IStreamDispatcher>();

        var stream = streamDispatcher.Stream(_requestFlowRequest, CancellationToken.None);

        return await BenchmarkWorkload.Consume(stream).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task<int> DispatchR()
    {
        using IServiceScope scope = _dispatchR.Provider.CreateScope();

        var dispatcher = scope.ServiceProvider
            .GetRequiredService<global::DispatchR.IMediator>();

        var stream = dispatcher.CreateStream(_dispatchRRequest, CancellationToken.None);

        return await BenchmarkWorkload.Consume(stream).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _dispatchR.Dispose();
    }
}
