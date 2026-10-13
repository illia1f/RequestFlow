using BenchmarkDotNet.Attributes;
using MediatR;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;
using RequestFlow.Benchmarks.RequestFlowFixtures.Catalog;

namespace RequestFlow.Benchmarks.MediatR;

[BenchmarkScope(ScopeLifetime.RetainedScope)]
public class MediatRCatalogSizeBenchmarks
{
    [Params(4, 16, 64)]
    public int CatalogSize { get; set; }

    [Params(CompletionKind.Cached, CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    private RetainedScopeHost<IRequestDispatcher> _requestFlow = null!;
    private RetainedScopeHost<ISender> _mediatR = null!;

    private RuntimeTaskRequest<RuntimeTag1> _requestFlow1 = null!;
    private MediatRCatalogRequest<RuntimeTag1> _mediatR1 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlow = new(RequestFlowSetup.Build(RequestFlowSetup.CatalogServices(CatalogSize, MediatRSetup.DispatcherLifetime)));
        _mediatR = new(MediatRSetup.Build(MediatRSetup.CatalogServices(CatalogSize)));

        _requestFlow1 = new RuntimeTaskRequest<RuntimeTag1>(Completion);
        _mediatR1 = new MediatRCatalogRequest<RuntimeTag1>(Completion);

        RequestFlow().GetAwaiter().GetResult();
        MediatR().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public Task<int> RequestFlow()
        => _requestFlow.Service.SendAsync(_requestFlow1, CancellationToken.None);

    [Benchmark]
    public Task<int> MediatR()
        => _mediatR.Service.Send(_mediatR1, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _mediatR.Dispose();
    }
}
