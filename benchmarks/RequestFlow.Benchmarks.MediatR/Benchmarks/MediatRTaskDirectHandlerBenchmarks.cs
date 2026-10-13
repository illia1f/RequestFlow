using BenchmarkDotNet.Attributes;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks.MediatR;

[BenchmarkScope(ScopeLifetime.DirectHandler)]
public class MediatRTaskDirectHandlerBenchmarks
{
    [Params(CompletionKind.Cached, CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    private readonly ScopeWitness _requestFlowWitness = new();
    private readonly ScopeWitness _mediatRWitness = new();

    private TaskRequestHandler _requestFlowHandler = null!;
    private MediatRTaskHandler _mediatRHandler = null!;

    private TaskRequest _requestFlowRequest = null!;
    private MediatRTaskRequest _mediatRRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlowHandler = new TaskRequestHandler(_requestFlowWitness);
        _mediatRHandler = new MediatRTaskHandler(_mediatRWitness);

        _requestFlowRequest = new TaskRequest(Completion);
        _mediatRRequest = new MediatRTaskRequest(Completion);

        RequestFlowDirectHandler().GetAwaiter().GetResult();
        MediatRDirectHandler().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public Task<int> RequestFlowDirectHandler()
        => _requestFlowHandler.HandleAsync(_requestFlowRequest, CancellationToken.None);

    [Benchmark]
    public Task<int> MediatRDirectHandler()
        => _mediatRHandler.Handle(_mediatRRequest, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlowHandler.Dispose();
        _mediatRHandler.Dispose();

        _requestFlowWitness.Dispose();
        _mediatRWitness.Dispose();
    }
}
