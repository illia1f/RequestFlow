using System.Reflection;
using BenchmarkDotNet.Attributes;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;
using RequestFlow.Benchmarks.RequestFlowFixtures.Catalog;

namespace RequestFlow.Benchmarks.DispatchR;

[BenchmarkScope(ScopeLifetime.RetainedScope)]
public class DispatchRMixedTrafficBenchmarks
{
    private static readonly MethodInfo BindDispatchRSendDefinition =
        typeof(DispatchRMixedTrafficBenchmarks).GetMethod(nameof(BindDispatchRSend), BindingFlags.NonPublic | BindingFlags.Static)!;

    [Params(4, 16, 64)]
    public int CatalogSize { get; set; }

    [Params(CompletionKind.Cached, CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    private RetainedScopeHost<global::RequestFlow.IRequestDispatcher> _requestFlow = null!;
    private RetainedScopeHost<global::DispatchR.IMediator> _dispatchR = null!;

    private global::RequestFlow.IRequest<int>[] _requestFlowRequests = null!;
    private WorkloadMessage[] _dispatchRRequests = null!;

    private Func<global::RequestFlow.IRequestDispatcher, Task<int>>[] _requestFlowSends = null!;
    private Func<global::DispatchR.IMediator, Task<int>>[] _dispatchRSends = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlow = new(RequestFlowSetup.Build(RequestFlowSetup.CatalogServices(CatalogSize)));
        _dispatchR = new(DispatchRSetup.Build(DispatchRSetup.CatalogServices(CatalogSize)));

        IReadOnlyList<Type> order = RuntimeCatalog.MixedTrafficOrder(CatalogSize);

        _requestFlowRequests = CreateRequests<global::RequestFlow.IRequest<int>>(typeof(RuntimeTaskRequest<>), order);
        _dispatchRRequests = CreateRequests<WorkloadMessage>(typeof(DispatchRCatalogRequest<>), order);

        _requestFlowSends = [.. _requestFlowRequests.Select(RequestFlowSend)];
        _dispatchRSends = [.. _dispatchRRequests.Select(DispatchRSend)];

        RequestFlow().GetAwaiter().GetResult();
        DispatchR().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = RuntimeCatalog.MixedTrafficBatch)]
    public async Task<int> RequestFlow()
    {
        int sum = 0;

        foreach (Func<global::RequestFlow.IRequestDispatcher, Task<int>> send in _requestFlowSends)
            sum += await send(_requestFlow.Service).ConfigureAwait(false);

        return sum;
    }

    [Benchmark(OperationsPerInvoke = RuntimeCatalog.MixedTrafficBatch)]
    public async Task<int> DispatchR()
    {
        int sum = 0;

        foreach (Func<global::DispatchR.IMediator, Task<int>> send in _dispatchRSends)
            sum += await send(_dispatchR.Service).ConfigureAwait(false);

        return sum;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _dispatchR.Dispose();
    }

    private TRequest[] CreateRequests<TRequest>(Type requestDefinition, IReadOnlyList<Type> order)
        => [.. order.Select(tag => (TRequest)Activator.CreateInstance(requestDefinition.MakeGenericType(tag), Completion)!)];

    private static Func<global::RequestFlow.IRequestDispatcher, Task<int>> RequestFlowSend(global::RequestFlow.IRequest<int> request)
        => dispatcher => dispatcher.SendAsync(request, CancellationToken.None);

    // DispatchR resolves the handler from the compile-time request type, so each send is bound to its tag here.
    private static Func<global::DispatchR.IMediator, Task<int>> DispatchRSend(WorkloadMessage request)
        => (Func<global::DispatchR.IMediator, Task<int>>)BindDispatchRSendDefinition
            .MakeGenericMethod(request.GetType().GetGenericArguments()[0])
            .Invoke(null, [request])!;

    private static Func<global::DispatchR.IMediator, Task<int>> BindDispatchRSend<TTag>(DispatchRCatalogRequest<TTag> request)
        => mediator => mediator.Send(request, CancellationToken.None);
}
