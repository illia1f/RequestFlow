using BenchmarkDotNet.Attributes;
using MediatR;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks.MediatR;

[BenchmarkScope(ScopeLifetime.RetainedScope)]
public class MediatRVoidRequestRetainedScopeBenchmarks
{
    [Params(CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    [Params(0, 1, 5)]
    public int StageCount { get; set; }

    private RetainedScopeHost<IRequestDispatcher> _requestFlow = null!;
    private RetainedScopeHost<ISender> _mediatR = null!;

    private TaskVoidRequest _requestFlowRequest = null!;
    private MediatRVoidRequest _mediatRRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlow = new(RequestFlowSetup.Build(RequestFlowSetup.TaskVoidRequestServices(StageCount, MediatRSetup.DispatcherLifetime)));
        _mediatR = new(MediatRSetup.Build(MediatRSetup.VoidRequestServices(StageCount)));

        _requestFlowRequest = new TaskVoidRequest(Completion);
        _mediatRRequest = new MediatRVoidRequest(Completion);

        RequestFlow().GetAwaiter().GetResult();
        MediatR().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public Task RequestFlow()
        => _requestFlow.Service.SendAsync(_requestFlowRequest, CancellationToken.None);

    [Benchmark]
    public Task MediatR()
        => _mediatR.Service.Send(_mediatRRequest, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _mediatR.Dispose();
    }
}
