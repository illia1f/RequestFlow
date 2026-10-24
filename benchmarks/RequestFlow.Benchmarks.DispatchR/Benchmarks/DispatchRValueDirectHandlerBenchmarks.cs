using BenchmarkDotNet.Attributes;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks.DispatchR;

[BenchmarkScope(ScopeLifetime.DirectHandler)]
public class DispatchRValueDirectHandlerBenchmarks
{
    [Params(CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    private readonly ScopeWitness _requestFlowWitness = new();
    private readonly ScopeWitness _dispatchRWitness = new();

    private ValueRequestHandler _requestFlowHandler = null!;
    private DispatchRValueHandler _dispatchRHandler = null!;

    private ValueRequest _requestFlowRequest = null!;
    private DispatchRValueRequest _dispatchRRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlowHandler = new ValueRequestHandler(_requestFlowWitness);
        _dispatchRHandler = new DispatchRValueHandler(_dispatchRWitness);

        _requestFlowRequest = new ValueRequest(Completion);
        _dispatchRRequest = new DispatchRValueRequest(Completion);

        RequestFlowDirectHandler().GetAwaiter().GetResult();
        DispatchRDirectHandler().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public ValueTask<int> RequestFlowDirectHandler()
        => _requestFlowHandler.HandleAsync(_requestFlowRequest, CancellationToken.None);

    [Benchmark]
    public ValueTask<int> DispatchRDirectHandler()
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
