using BenchmarkDotNet.Attributes;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;
using RequestFlow.Benchmarks.RequestFlowFixtures.Catalog;

namespace RequestFlow.Benchmarks.DispatchR;

[BenchmarkScope(ScopeLifetime.RetainedScope)]
public class DispatchRCatalogSizeBenchmarks
{
    [Params(4, 16, 64)]
    public int CatalogSize { get; set; }

    [Params(CompletionKind.Cached, CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    private RetainedScopeHost<global::RequestFlow.IRequestDispatcher> _requestFlow = null!;
    private RetainedScopeHost<global::DispatchR.IMediator> _dispatchR = null!;

    private RuntimeTaskRequest<RuntimeTag1> _requestFlow1 = null!;
    private DispatchRCatalogRequest<RuntimeTag1> _dispatchR1 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlow = new(RequestFlowSetup.Build(RequestFlowSetup.CatalogServices(CatalogSize)));
        _dispatchR = new(DispatchRSetup.Build(DispatchRSetup.CatalogServices(CatalogSize)));

        _requestFlow1 = new RuntimeTaskRequest<RuntimeTag1>(Completion);
        _dispatchR1 = new DispatchRCatalogRequest<RuntimeTag1>(Completion);

        RequestFlow().GetAwaiter().GetResult();
        DispatchR().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public Task<int> RequestFlow()
        => _requestFlow.Service.SendAsync(_requestFlow1, CancellationToken.None);

    [Benchmark]
    public Task<int> DispatchR()
        => _dispatchR.Service.Send(_dispatchR1, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _dispatchR.Dispose();
    }
}
