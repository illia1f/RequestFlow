using BenchmarkDotNet.Attributes;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks.MediatR;

[BenchmarkScope(ScopeLifetime.ScopePerOperation)]
public class MediatRTaskScopePerOperationBenchmarks
{
    [Params(CompletionKind.Cached, CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    [Params(0, 1, 5)]
    public int StageCount { get; set; }

    private ScopePerOperationHost _requestFlow = null!;
    private ScopePerOperationHost _mediatR = null!;

    private TaskRequest _requestFlowRequest = null!;
    private MediatRTaskRequest _mediatRRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlow = new(RequestFlowSetup.Build(RequestFlowSetup.TaskServices(StageCount, MediatRSetup.DispatcherLifetime)));
        _mediatR = new(MediatRSetup.Build(MediatRSetup.TaskServices(StageCount)));

        _requestFlowRequest = new TaskRequest(Completion);
        _mediatRRequest = new MediatRTaskRequest(Completion);

        RequestFlow().GetAwaiter().GetResult();
        MediatR().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public async Task<int> RequestFlow()
    {
        using IServiceScope scope = _requestFlow.Provider.CreateScope();

        var dispatcher = scope.ServiceProvider
            .GetRequiredService<IRequestDispatcher>();

        return await dispatcher
            .SendAsync<int>(_requestFlowRequest, CancellationToken.None).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task<int> MediatR()
    {
        using IServiceScope scope = _mediatR.Provider.CreateScope();

        var dispatcher = scope.ServiceProvider
            .GetRequiredService<ISender>();

        return await dispatcher
            .Send(_mediatRRequest, CancellationToken.None).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _mediatR.Dispose();
    }
}
