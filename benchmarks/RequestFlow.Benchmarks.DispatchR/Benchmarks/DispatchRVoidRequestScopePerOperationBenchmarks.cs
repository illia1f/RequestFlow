using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks.DispatchR;

[BenchmarkScope(ScopeLifetime.ScopePerOperation)]
public class DispatchRVoidRequestScopePerOperationBenchmarks
{
    [Params(CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    [Params(0, 1, 5)]
    public int StageCount { get; set; }

    private ScopePerOperationHost _requestFlow = null!;
    private ScopePerOperationHost _dispatchR = null!;

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
    public async Task RequestFlow()
    {
        using IServiceScope scope = _requestFlow.Provider.CreateScope();

        var dispatcher = scope.ServiceProvider
            .GetRequiredService<global::RequestFlow.IRequestDispatcher>();

        await dispatcher
            .SendAsync(_requestFlowRequest, CancellationToken.None).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task<DispatchRVoidRequestResult> DispatchR()
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
