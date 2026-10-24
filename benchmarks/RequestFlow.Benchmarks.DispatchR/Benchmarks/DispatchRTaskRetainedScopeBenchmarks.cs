using BenchmarkDotNet.Attributes;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks.DispatchR;

[BenchmarkScope(ScopeLifetime.RetainedScope)]
public class DispatchRTaskRetainedScopeBenchmarks
{
    [Params(CompletionKind.Cached, CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    [Params(0, 1, 5)]
    public int StageCount { get; set; }

    private RetainedScopeHost<global::RequestFlow.IRequestDispatcher> _requestFlow = null!;
    private RetainedScopeHost<global::DispatchR.IMediator> _dispatchR = null!;

    private TaskRequest _requestFlowRequest = null!;
    private DispatchRTaskRequest _dispatchRRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlow = new(RequestFlowSetup.Build(RequestFlowSetup.TaskServices(StageCount)));
        _dispatchR = new(DispatchRSetup.Build(DispatchRSetup.TaskServices(StageCount)));

        _requestFlowRequest = new TaskRequest(Completion);
        _dispatchRRequest = new DispatchRTaskRequest(Completion);

        RequestFlow().GetAwaiter().GetResult();
        DispatchR().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public Task<int> RequestFlow()
        => _requestFlow.Service.SendAsync<int>(_requestFlowRequest, CancellationToken.None);

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
