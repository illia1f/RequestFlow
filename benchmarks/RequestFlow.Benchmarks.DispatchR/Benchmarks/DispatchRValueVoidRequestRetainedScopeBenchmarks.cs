using BenchmarkDotNet.Attributes;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks.DispatchR;

[BenchmarkScope(ScopeLifetime.RetainedScope)]
public class DispatchRValueVoidRequestRetainedScopeBenchmarks
{
    [Params(CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    [Params(0, 1, 5)]
    public int StageCount { get; set; }

    private RetainedScopeHost<global::RequestFlow.IValueRequestDispatcher> _requestFlow = null!;
    private RetainedScopeHost<global::DispatchR.IMediator> _dispatchR = null!;

    private ValueVoidRequest _requestFlowRequest = null!;
    private DispatchRValueVoidRequest _dispatchRRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlow = new(RequestFlowSetup.Build(RequestFlowSetup.ValueVoidRequestServices(StageCount)));
        _dispatchR = new(DispatchRSetup.Build(DispatchRSetup.ValueVoidRequestServices(StageCount)));

        _requestFlowRequest = new ValueVoidRequest(Completion);
        _dispatchRRequest = new DispatchRValueVoidRequest(Completion);

        RequestFlow().GetAwaiter().GetResult();
        DispatchR().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public ValueTask RequestFlow()
        => _requestFlow.Service.SendAsync(_requestFlowRequest, CancellationToken.None);

    [Benchmark]
    public ValueTask<DispatchRVoidRequestResult> DispatchR()
        => _dispatchR.Service.Send(_dispatchRRequest, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _dispatchR.Dispose();
    }
}
