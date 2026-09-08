using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class RequestFlowRegistryTests
{
    [Theory]
    [InlineData(typeof(Echo), true, typeof(StagedRequestPlan<Echo, string>))]
    [InlineData(typeof(Purge), true, typeof(StagedVoidRequestPlan<Purge>))]
    [InlineData(typeof(Echo), false, typeof(RequestPlan<Echo, string>))]
    public void Given_Request_Stages_When_Building_The_Dispatch_Map_Then_Selects_The_Matching_Plan(
        Type requestType, bool addStage, Type expectedPlanType)
    {
        using ServiceProvider provider = BuildProvider(o =>
        {
            if (addStage)
                o.AddStage(typeof(WrapStage<,>));
        });
        DispatchMap map = provider.GetRequiredService<DispatchMap>();

        bool found = map.TryGetPlanFor(requestType, out RequestPlanBase? plan);

        found.ShouldBeTrue();
        plan.ShouldBeOfType(expectedPlanType);
    }

    // A plan builds its levels when the map freezes, so a dispatch reaches that same plan: it asks
    // the container for the levels in chain order and for nothing the freeze itself needed.
    // Building a plan per call would take the registry, which holds the reflection, and show up here.
    [Fact]
    public async Task Given_A_Staged_Request_When_Dispatching_Twice_Then_Both_Calls_Only_Resolve_The_Frozen_Levels()
    {
        using ServiceProvider provider = BuildProvider(o => o.AddStage(typeof(WrapStage<,>)));
        using IServiceScope scope = provider.CreateScope();
        var counting = new CountingProvider(scope.ServiceProvider);
        var dispatcher = new RequestDispatcher(
            scope.ServiceProvider.GetRequiredService<DispatchMap>(), counting);

        await dispatcher.SendAsync(new Echo("hi"));
        await dispatcher.SendAsync(new Echo("hi"));

        counting.Requested.ShouldBe(
        [
            typeof(WrapStage<Echo, string>), typeof(IRequestHandler<Echo, string>),
            typeof(WrapStage<Echo, string>), typeof(IRequestHandler<Echo, string>),
        ]);
    }

    #region Helpers

    private static ServiceProvider BuildProvider(Action<RequestFlowOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<RequestFlowRegistryTests>();
            configure?.Invoke(o);
        });

        return services.BuildServiceProvider();
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
            TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    #endregion
}
