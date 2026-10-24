using BenchmarkDotNet.Attributes;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks.DispatchR;

[BenchmarkScope(ScopeLifetime.DirectHandler)]
public class DispatchRTaskDirectHandlerBenchmarks
{
    [Params(CompletionKind.Cached, CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    private readonly ScopeWitness _requestFlowWitness = new();
    private readonly ScopeWitness _dispatchRWitness = new();

    private TaskRequestHandler _requestFlowHandler = null!;
    private DispatchRTaskHandler _dispatchRHandler = null!;

    private TaskRequest _requestFlowRequest = null!;
    private DispatchRTaskRequest _dispatchRRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlowHandler = new TaskRequestHandler(_requestFlowWitness);
        _dispatchRHandler = new DispatchRTaskHandler(_dispatchRWitness);

        _requestFlowRequest = new TaskRequest(Completion);
        _dispatchRRequest = new DispatchRTaskRequest(Completion);

        RequestFlowDirectHandler().GetAwaiter().GetResult();
        DispatchRDirectHandler().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public Task<int> RequestFlowDirectHandler()
        => _requestFlowHandler.HandleAsync(_requestFlowRequest, CancellationToken.None);

    [Benchmark]
    public Task<int> DispatchRDirectHandler()
        => _dispatchRHandler.Handle(_dispatchRRequest, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlowHandler.Dispose();
        _dispatchRHandler.Dispose();

        _requestFlowWitness.Dispose();
        _dispatchRWitness.Dispose();
    }
}
