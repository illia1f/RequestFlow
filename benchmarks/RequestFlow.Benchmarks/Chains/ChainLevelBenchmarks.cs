using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks;

/// <summary>
/// The Task and ValueTask plans that <c>AddRequestFlow</c> froze, entered directly, so each row holds the stage chain and not the dispatcher around it.
/// Each stage <see cref="StageCount"/> adds costs a DI lookup and the fixture stage body as well as the chain step.
/// </summary>
[BenchmarkScope(ScopeLifetime.RetainedScope)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ChainLevelBenchmarks
{
    private const string ResponseCategory = "Response";
    private const string VoidCategory = "Void";

    [Params(0, 1, 5)]
    public int StageCount { get; set; }

    private PlanHost<RequestPlan<int>> _task = null!;
    private PlanHost<ValueRequestPlan<int>> _value = null!;
    private PlanHost<RequestPlan<NoResult>> _taskVoid = null!;
    private PlanHost<ValueVoidRequestPlan> _valueVoid = null!;

    private RequestPlan<int> _taskPlan = null!;
    private ValueRequestPlan<int> _valuePlan = null!;
    private RequestPlan<NoResult> _taskVoidPlan = null!;
    private ValueVoidRequestPlan _valueVoidPlan = null!;

    private IServiceProvider _taskServices = null!;
    private IServiceProvider _valueServices = null!;
    private IServiceProvider _taskVoidServices = null!;
    private IServiceProvider _valueVoidServices = null!;

    private readonly TaskRequest _taskRequest = new();
    private readonly ValueRequest _valueRequest = new();
    private readonly TaskVoidRequest _taskVoidRequest = new();
    private readonly ValueVoidRequest _valueVoidRequest = new();

    [GlobalSetup]
    public void Setup()
    {
        _task = new(RequestFlowSetup.TaskServices(StageCount), typeof(TaskRequest));
        _value = new(RequestFlowSetup.ValueServices(StageCount), typeof(ValueRequest));
        _taskVoid = new(RequestFlowSetup.TaskVoidRequestServices(StageCount), typeof(TaskVoidRequest));
        _valueVoid = new(RequestFlowSetup.ValueVoidRequestServices(StageCount), typeof(ValueVoidRequest));

        (_taskPlan, _taskServices) = (_task.Plan, _task.Services);
        (_valuePlan, _valueServices) = (_value.Plan, _value.Services);
        (_taskVoidPlan, _taskVoidServices) = (_taskVoid.Plan, _taskVoid.Services);
        (_valueVoidPlan, _valueVoidServices) = (_valueVoid.Plan, _valueVoid.Services);

        TaskChain().GetAwaiter().GetResult();
        ValueChain().GetAwaiter().GetResult();
        TaskVoidChain().GetAwaiter().GetResult();
        ValueVoidChain().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true), BenchmarkCategory(ResponseCategory)]
    public Task<int> TaskChain()
        => _taskPlan.ExecuteAsync(_taskRequest, _taskServices, CancellationToken.None);

    [Benchmark, BenchmarkCategory(ResponseCategory)]
    public ValueTask<int> ValueChain()
        => _valuePlan.ExecuteAsync(_valueRequest, _valueServices, CancellationToken.None);

    [Benchmark(Baseline = true), BenchmarkCategory(VoidCategory)]
    public Task TaskVoidChain()
        => _taskVoidPlan.ExecuteAsync(_taskVoidRequest, _taskVoidServices, CancellationToken.None);

    [Benchmark, BenchmarkCategory(VoidCategory)]
    public ValueTask ValueVoidChain()
        => _valueVoidPlan.ExecuteVoidAsync(_valueVoidRequest, _valueVoidServices, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _task.Dispose();
        _value.Dispose();
        _taskVoid.Dispose();
        _valueVoid.Dispose();
    }

    // A provider built from the fixture registrations, one retained scope, and the plan the provider froze for one request type.
    private sealed class PlanHost<TPlan> : IDisposable
        where TPlan : RequestPlanBase
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        public PlanHost(ServiceCollection services, Type requestType)
        {
            _provider = RequestFlowSetup.Build(services);
            _scope = _provider.CreateScope();

            Plan = _provider.GetRequiredService<DispatchMap>().TryGetPlanFor(requestType, out RequestPlanBase? plan)
                ? (TPlan)plan
                : throw new InvalidOperationException($"RequestFlow froze no plan for '{requestType.Name}'.");
        }

        public TPlan Plan { get; }

        public IServiceProvider Services => _scope.ServiceProvider;

        public void Dispose()
        {
            _scope.Dispose();
            _provider.Dispose();
        }
    }
}
