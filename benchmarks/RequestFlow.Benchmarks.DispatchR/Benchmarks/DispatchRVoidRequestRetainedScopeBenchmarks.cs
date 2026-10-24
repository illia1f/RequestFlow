using BenchmarkDotNet.Attributes;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks.DispatchR;

[BenchmarkScope(ScopeLifetime.RetainedScope)]
public class DispatchRVoidRequestRetainedScopeBenchmarks
{
    [Params(CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    [Params(0, 1, 5)]
    public int StageCount { get; set; }

    private RetainedScopeHost<global::RequestFlow.IRequestDispatcher> _requestFlow = null!;
    private RetainedScopeHost<global::DispatchR.IMediator> _dispatchR = null!;

    private TaskVoidRequest _requestFlowRequest = null!;
    private DispatchRVoidRequest _dispatchRRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlow = new(RequestFlowSetup.Build(RequestFlowSetup.TaskVoidRequestServices(StageCount)));
        _dispatchR = new(DispatchRSetup.Build(DispatchRSetup.VoidRequestServices(StageCount)));

        _requestFlowRequest = new TaskVoidRequest(Completion);
        _dispatchRRequest = new DispatchRVoidRequest(Completion);

        RequestFlow().GetAwaiter().GetResult();
        DispatchR().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public Task RequestFlow()
        => _requestFlow.Service.SendAsync(_requestFlowRequest, CancellationToken.None);

    [Benchmark]
    public Task<DispatchRVoidRequestResult> DispatchR()
        => _dispatchR.Service.Send(_dispatchRRequest, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _dispatchR.Dispose();
    }
}
