using BenchmarkDotNet.Attributes;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks.DispatchR;

[BenchmarkScope(ScopeLifetime.RetainedScope)]
public class DispatchRStreamRetainedScopeBenchmarks
{
    [Params(1, 10, 100)]
    public int ItemCount { get; set; }

    [Params(CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    [Params(0, 1, 5)]
    public int StageCount { get; set; }

    private RetainedScopeHost<global::RequestFlow.IStreamDispatcher> _requestFlow = null!;
    private RetainedScopeHost<global::DispatchR.IMediator> _dispatchR = null!;

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
    public Task<int> RequestFlow()
        => BenchmarkWorkload.Consume(_requestFlow.Service.Stream(_requestFlowRequest, CancellationToken.None));

    [Benchmark]
    public Task<int> DispatchR()
        => BenchmarkWorkload.Consume(_dispatchR.Service.CreateStream(_dispatchRRequest, CancellationToken.None));

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _dispatchR.Dispose();
    }
}
