using BenchmarkDotNet.Attributes;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks.MediatR;

[BenchmarkScope(ScopeLifetime.ScopePerOperation)]
public class MediatREventSequentialScopePerOperationBenchmarks
{
    [Params(CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    [Params(1, 5)]
    public int HandlerCount { get; set; }

    private ScopePerOperationHost _requestFlow = null!;
    private ScopePerOperationHost _mediatR = null!;

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
    public async Task RequestFlow()
    {
        using IServiceScope scope = _requestFlow.Provider.CreateScope();

        var publisher = scope.ServiceProvider
            .GetRequiredService<IEventPublisher>();

        await publisher
            .PublishAsync(_requestFlowRequest, CancellationToken.None).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task MediatR()
    {
        using IServiceScope scope = _mediatR.Provider.CreateScope();

        var publisher = scope.ServiceProvider
            .GetRequiredService<IPublisher>();

        await publisher
            .Publish(_mediatRRequest, CancellationToken.None).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _mediatR.Dispose();
    }
}
