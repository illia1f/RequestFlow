using BenchmarkDotNet.Attributes;
using MediatR;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;
using RequestFlow.Benchmarks.RequestFlowFixtures.Catalog;

namespace RequestFlow.Benchmarks.MediatR;

[BenchmarkScope(ScopeLifetime.RetainedScope)]
public class MediatRMixedTrafficBenchmarks
{
    [Params(4, 16, 64)]
    public int CatalogSize { get; set; }

    [Params(CompletionKind.Cached, CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    private RetainedScopeHost<IRequestDispatcher> _requestFlow = null!;
    private RetainedScopeHost<ISender> _mediatR = null!;

    private global::RequestFlow.IRequest<int>[] _requestFlowRequests = null!;
    private global::MediatR.IRequest<int>[] _mediatRRequests = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlow = new(RequestFlowSetup.Build(RequestFlowSetup.CatalogServices(CatalogSize, MediatRSetup.DispatcherLifetime)));
        _mediatR = new(MediatRSetup.Build(MediatRSetup.CatalogServices(CatalogSize)));

        IReadOnlyList<Type> order = RuntimeCatalog.MixedTrafficOrder(CatalogSize);

        _requestFlowRequests = CreateRequests<global::RequestFlow.IRequest<int>>(typeof(RuntimeTaskRequest<>), order);
        _mediatRRequests = CreateRequests<global::MediatR.IRequest<int>>(typeof(MediatRCatalogRequest<>), order);

        RequestFlow().GetAwaiter().GetResult();
        MediatR().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = RuntimeCatalog.MixedTrafficBatch)]
    public async Task<int> RequestFlow()
    {
        int sum = 0;

        foreach (global::RequestFlow.IRequest<int> request in _requestFlowRequests)
            sum += await _requestFlow.Service.SendAsync(request, CancellationToken.None).ConfigureAwait(false);

        return sum;
    }

    [Benchmark(OperationsPerInvoke = RuntimeCatalog.MixedTrafficBatch)]
    public async Task<int> MediatR()
    {
        int sum = 0;

        foreach (global::MediatR.IRequest<int> request in _mediatRRequests)
            sum += await _mediatR.Service.Send(request, CancellationToken.None).ConfigureAwait(false);

        return sum;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _mediatR.Dispose();
    }

    private TRequest[] CreateRequests<TRequest>(Type requestDefinition, IReadOnlyList<Type> order)
        => [.. order.Select(tag => (TRequest)Activator.CreateInstance(requestDefinition.MakeGenericType(tag), Completion)!)];
}
