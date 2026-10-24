using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks.DispatchR;

[BenchmarkScope(ScopeLifetime.ScopePerOperation)]
public class DispatchREventSequentialScopePerOperationBenchmarks
{
    [Params(CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    [Params(1, 5)]
    public int HandlerCount { get; set; }

    private ScopePerOperationHost _requestFlow = null!;
    private ScopePerOperationHost _dispatchR = null!;

    private EventMessage _requestFlowRequest = null!;
    private DispatchREvent _dispatchRRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlow = new(RequestFlowSetup.Build(RequestFlowSetup.EventServices(HandlerCount, parallel: false)));
        _dispatchR = new(DispatchRSetup.Build(DispatchRSetup.EventServices(HandlerCount)));

        _requestFlowRequest = new EventMessage(Completion);
        _dispatchRRequest = new DispatchREvent(Completion);

        RequestFlow().GetAwaiter().GetResult();
        DispatchR().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public async Task RequestFlow()
    {
        using IServiceScope scope = _requestFlow.Provider.CreateScope();

        var publisher = scope.ServiceProvider
            .GetRequiredService<global::RequestFlow.IEventPublisher>();

        await publisher
            .PublishAsync(_requestFlowRequest, CancellationToken.None).ConfigureAwait(false);
    }

    [Benchmark]
    public async ValueTask DispatchR()
    {
        using IServiceScope scope = _dispatchR.Provider.CreateScope();

        var publisher = scope.ServiceProvider
            .GetRequiredService<global::DispatchR.IMediator>();

        await publisher
            .Publish(_dispatchRRequest, CancellationToken.None).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _dispatchR.Dispose();
    }
}
