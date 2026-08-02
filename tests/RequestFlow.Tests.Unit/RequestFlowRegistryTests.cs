using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class RequestFlowRegistryTests
{
    [Fact]
    public void Given_One_Applicable_Stage_When_Building_The_Dispatch_Map_Then_Request_Gets_The_Staged_Plan()
    {
        DispatchMap map = BuildMap(o => o.AddStage(typeof(WrapStage<,>)));

        map.TryGet(typeof(Echo), out RequestPlanBase? plan);

        plan.ShouldBeOfType<StagedRequestPlan<Echo, string>>();
    }

    [Fact]
    public void Given_One_Applicable_Stage_When_Building_The_Dispatch_Map_Then_Void_Request_Gets_The_Staged_Void_Plan()
    {
        DispatchMap map = BuildMap(o => o.AddStage(typeof(WrapStage<,>)));

        map.TryGet(typeof(Purge), out RequestPlanBase? plan);

        plan.ShouldBeOfType<StagedVoidRequestPlan<Purge>>();
    }

    [Fact]
    public void Given_Two_Applicable_Stages_When_Building_The_Dispatch_Map_Then_Request_Gets_The_General_Staged_Plan()
    {
        DispatchMap map = BuildMap(o => o.AddStage(typeof(WrapStage<,>)).AddStage(typeof(ExtraStage<,>)));

        map.TryGet(typeof(Echo), out RequestPlanBase? plan);

        plan.ShouldBeOfType<StagedRequestPlan<Echo, string>>();
    }

    [Fact]
    public void Given_Two_Applicable_Stages_When_Building_The_Dispatch_Map_Then_Void_Request_Gets_The_General_Staged_Void_Plan()
    {
        DispatchMap map = BuildMap(o => o.AddStage(typeof(WrapStage<,>)).AddStage(typeof(ExtraStage<,>)));

        map.TryGet(typeof(Purge), out RequestPlanBase? plan);

        plan.ShouldBeOfType<StagedVoidRequestPlan<Purge>>();
    }

    [Fact]
    public void Given_No_Stages_When_Building_The_Dispatch_Map_Then_Request_Gets_The_Plain_Plan()
    {
        DispatchMap map = BuildMap();

        map.TryGet(typeof(Echo), out RequestPlanBase? plan);

        plan.ShouldBeOfType<RequestPlan<Echo, string>>();
    }

    #region Helpers

    private static DispatchMap BuildMap(Action<RequestFlowOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<RequestFlowRegistryTests>();
            configure?.Invoke(o);
        });

        return services.BuildServiceProvider().GetRequiredService<DispatchMap>();
    }

    public sealed record Echo(string Text) : IRequest<string>;

    public sealed record Purge : IRequest;

    public sealed class EchoHandler : IRequestHandler<Echo, string>
    {
        public Task<string> HandleAsync(Echo request, CancellationToken cancellationToken)
            => Task.FromResult(request.Text);
    }

    public sealed class PurgeHandler : IRequestHandler<Purge>
    {
        public Task HandleAsync(Purge request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class WrapStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request, IContinuation<TResponse> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    public sealed class ExtraStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request, IContinuation<TResponse> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    #endregion
}
