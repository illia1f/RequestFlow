using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

/// <summary>
/// Every level asks the container for its stage or handler on every entry, whatever lifetime it was
/// registered with. Scoped is the case worth pinning, since the container hands back the same
/// instance either way and only the count of asks tells a resolution apart from a cache.
/// </summary>
public sealed class LevelResolutionTests
{
    [Fact]
    public async Task Given_A_Scoped_Chain_When_Dispatching_Twice_Then_Every_Level_Is_Resolved_Again()
    {
        using IServiceScope scope = ScopeOver(typeof(FirstStage), typeof(SecondStage));
        RequestDispatcher dispatcher = CountingDispatcher(scope, out CountingProvider counting);

        await dispatcher.SendAsync(new Resolved());
        counting.Requested.Clear();
        await dispatcher.SendAsync(new Resolved());

        counting.Requested.ShouldBe(
            [typeof(FirstStage), typeof(SecondStage), typeof(IRequestHandler<Resolved, string>)]);
    }

    [Fact]
    public async Task Given_A_Scoped_Void_Chain_When_Dispatching_Twice_Then_Every_Level_Is_Resolved_Again()
    {
        using IServiceScope scope = ScopeOver(typeof(FirstVoidStage), typeof(SecondVoidStage));
        RequestDispatcher dispatcher = CountingDispatcher(scope, out CountingProvider counting);

        await dispatcher.SendAsync(new ResolvedVoid());
        counting.Requested.Clear();
        await dispatcher.SendAsync(new ResolvedVoid());

        counting.Requested.ShouldBe(
            [typeof(FirstVoidStage), typeof(SecondVoidStage), typeof(IRequestHandler<ResolvedVoid>)]);
    }

    // A plan with no stages reaches its handler through a level of its own, so it is pinned apart
    // from the chains above.
    [Fact]
    public async Task Given_A_Scoped_Handler_With_No_Stages_When_Dispatching_Twice_Then_It_Is_Resolved_Again()
    {
        using IServiceScope scope = ScopeOver();
        RequestDispatcher dispatcher = CountingDispatcher(scope, out CountingProvider counting);

        await dispatcher.SendAsync(new Resolved());
        counting.Requested.Clear();
        await dispatcher.SendAsync(new Resolved());

        counting.Requested.ShouldBe([typeof(IRequestHandler<Resolved, string>)]);
    }

    [Fact]
    public async Task Given_A_Scoped_Void_Handler_With_No_Stages_When_Dispatching_Twice_Then_It_Is_Resolved_Again()
    {
        using IServiceScope scope = ScopeOver();
        RequestDispatcher dispatcher = CountingDispatcher(scope, out CountingProvider counting);

        await dispatcher.SendAsync(new ResolvedVoid());
        counting.Requested.Clear();
        await dispatcher.SendAsync(new ResolvedVoid());

        counting.Requested.ShouldBe([typeof(IRequestHandler<ResolvedVoid>)]);
    }

    #region Helpers

    // Handlers and stages all scoped, so a level that resolved once could serve both dispatches.
    private static IServiceScope ScopeOver(params Type[] stageTypes)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<LevelResolutionTests>();
            o.WithScopedHandlers();
            foreach (Type stageType in stageTypes)
            {
                o.AddStage(stageType, s => s.AsScoped());
            }
        });

        return services.BuildServiceProvider().CreateScope();
    }

    private static RequestDispatcher CountingDispatcher(IServiceScope scope, out CountingProvider counting)
    {
        counting = new CountingProvider(scope.ServiceProvider);

        return new RequestDispatcher(scope.ServiceProvider.GetRequiredService<DispatchMap>(), counting);
    }

    public sealed record Resolved : IRequest<string>;

    public sealed class ResolvedHandler : IRequestHandler<Resolved, string>
    {
        public Task<string> HandleAsync(Resolved request, CancellationToken cancellationToken)
            => Task.FromResult("resolved");
    }

    public sealed record ResolvedVoid : IRequest;

    public sealed class ResolvedVoidHandler : IRequestHandler<ResolvedVoid>
    {
        public Task HandleAsync(ResolvedVoid request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class FirstStage : IRequestStage<Resolved, string>
    {
        public Task<string> HandleAsync(
            Resolved request, Continuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    public sealed class SecondStage : IRequestStage<Resolved, string>
    {
        public Task<string> HandleAsync(
            Resolved request, Continuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    public sealed class FirstVoidStage : IRequestStage<ResolvedVoid>
    {
        public Task HandleAsync(ResolvedVoid request, Continuation next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    public sealed class SecondVoidStage : IRequestStage<ResolvedVoid>
    {
        public Task HandleAsync(ResolvedVoid request, Continuation next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    #endregion
}
