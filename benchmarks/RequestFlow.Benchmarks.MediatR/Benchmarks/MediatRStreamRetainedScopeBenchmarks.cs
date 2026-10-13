using BenchmarkDotNet.Attributes;
using MediatR;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks.MediatR;

[BenchmarkScope(ScopeLifetime.RetainedScope)]
public class MediatRStreamRetainedScopeBenchmarks
{
    [Params(1, 10, 100)]
    public int ItemCount { get; set; }

    [Params(CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    [Params(0, 1, 5)]
    public int StageCount { get; set; }

    private RetainedScopeHost<IStreamDispatcher> _requestFlow = null!;
    private RetainedScopeHost<ISender> _mediatR = null!;

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
    public Task<int> RequestFlow()
        => BenchmarkWorkload.Consume(_requestFlow.Service.Stream(_requestFlowRequest, CancellationToken.None));

    [Benchmark]
    public Task<int> MediatR()
        => BenchmarkWorkload.Consume(_mediatR.Service.CreateStream(_mediatRRequest, CancellationToken.None));

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _mediatR.Dispose();
    }
}
