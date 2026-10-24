using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks.DispatchR;

[BenchmarkScope(ScopeLifetime.ScopePerOperation)]
public class DispatchRValueVoidRequestScopePerOperationBenchmarks
{
    [Params(CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    [Params(0, 1, 5)]
    public int StageCount { get; set; }

    private ScopePerOperationHost _requestFlow = null!;
    private ScopePerOperationHost _dispatchR = null!;

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
    public async ValueTask RequestFlow()
    {
        using IServiceScope scope = _requestFlow.Provider.CreateScope();

        var dispatcher = scope.ServiceProvider
            .GetRequiredService<global::RequestFlow.IValueRequestDispatcher>();

        await dispatcher
            .SendAsync(_requestFlowRequest, CancellationToken.None).ConfigureAwait(false);
    }

    [Benchmark]
    public async ValueTask<DispatchRVoidRequestResult> DispatchR()
    {
        using IServiceScope scope = _dispatchR.Provider.CreateScope();

        var dispatcher = scope.ServiceProvider
            .GetRequiredService<global::DispatchR.IMediator>();

        return await dispatcher
            .Send(_dispatchRRequest, CancellationToken.None).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _dispatchR.Dispose();
    }
}
