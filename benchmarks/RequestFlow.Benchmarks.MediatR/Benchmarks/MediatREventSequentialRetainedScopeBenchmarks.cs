using BenchmarkDotNet.Attributes;
using MediatR;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks.MediatR;

[BenchmarkScope(ScopeLifetime.RetainedScope)]
public class MediatREventSequentialRetainedScopeBenchmarks
{
    [Params(CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    [Params(1, 5)]
    public int HandlerCount { get; set; }

    private RetainedScopeHost<IEventPublisher> _requestFlow = null!;
    private RetainedScopeHost<IPublisher> _mediatR = null!;

    private EventMessage _requestFlowRequest = null!;
    private MediatREvent _mediatRRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlow = new(RequestFlowSetup.Build(
            RequestFlowSetup.EventServices(HandlerCount, parallel: false, dispatcherLifetime: MediatRSetup.DispatcherLifetime)));
        _mediatR = new(MediatRSetup.Build(MediatRSetup.EventServices(HandlerCount, parallel: false)));

        _requestFlowRequest = new EventMessage(Completion);
        _mediatRRequest = new MediatREvent(Completion);

        RequestFlow().GetAwaiter().GetResult();
        MediatR().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public Task RequestFlow()
        => _requestFlow.Service.PublishAsync(_requestFlowRequest, CancellationToken.None);

    [Benchmark]
    public Task MediatR()
        => _mediatR.Service.Publish(_mediatRRequest, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _mediatR.Dispose();
    }
}
