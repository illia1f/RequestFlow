using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class ValueStageLifetimeTests
{
    [Fact]
    public async Task Given_Transient_Typed_Value_Stage_When_Dispatching_Across_Scopes_Then_Every_Entry_Gets_A_New_Instance()
    {
        using ServiceProvider provider = BuildTypedProvider();

        await DispatchTypedThreeTimesAsync(provider);

        TypedInstances.Count.ShouldBe(3);
        TypedInstances[0].ShouldNotBeSameAs(TypedInstances[1]);
        TypedInstances[1].ShouldNotBeSameAs(TypedInstances[2]);
    }

    [Fact]
    public async Task Given_Scoped_Typed_Value_Stage_When_Dispatching_Across_Scopes_Then_Each_Scope_Gets_One_Instance()
    {
        using ServiceProvider provider = BuildTypedProvider(stage => stage.AsScoped());

        await DispatchTypedThreeTimesAsync(provider);

        TypedInstances.Count.ShouldBe(3);
        TypedInstances[0].ShouldBeSameAs(TypedInstances[1]);
        TypedInstances[1].ShouldNotBeSameAs(TypedInstances[2]);
    }

    [Fact]
    public async Task Given_Singleton_Typed_Value_Stage_When_Dispatching_Across_Scopes_Then_One_Instance_Serves_Every_Entry()
    {
        using ServiceProvider provider = BuildTypedProvider(stage => stage.AsSingleton());

        await DispatchTypedThreeTimesAsync(provider);

        TypedInstances.Count.ShouldBe(3);
        TypedInstances[0].ShouldBeSameAs(TypedInstances[1]);
        TypedInstances[1].ShouldBeSameAs(TypedInstances[2]);
    }

    [Fact]
    public async Task Given_Transient_Plain_Void_Value_Stage_When_Dispatching_Across_Scopes_Then_Every_Entry_Gets_A_New_Instance()
    {
        using ServiceProvider provider = BuildVoidProvider();

        await DispatchVoidThreeTimesAsync(provider);

        VoidInstances.Count.ShouldBe(3);
        VoidInstances[0].ShouldNotBeSameAs(VoidInstances[1]);
        VoidInstances[1].ShouldNotBeSameAs(VoidInstances[2]);
    }

    [Fact]
    public async Task Given_Scoped_Plain_Void_Value_Stage_When_Dispatching_Across_Scopes_Then_Each_Scope_Gets_One_Instance()
    {
        using ServiceProvider provider = BuildVoidProvider(stage => stage.AsScoped());

        await DispatchVoidThreeTimesAsync(provider);

        VoidInstances.Count.ShouldBe(3);
        VoidInstances[0].ShouldBeSameAs(VoidInstances[1]);
        VoidInstances[1].ShouldNotBeSameAs(VoidInstances[2]);
    }

    [Fact]
    public async Task Given_Singleton_Plain_Void_Value_Stage_When_Dispatching_Across_Scopes_Then_One_Instance_Serves_Every_Entry()
    {
        using ServiceProvider provider = BuildVoidProvider(stage => stage.AsSingleton());

        await DispatchVoidThreeTimesAsync(provider);

        VoidInstances.Count.ShouldBe(3);
        VoidInstances[0].ShouldBeSameAs(VoidInstances[1]);
        VoidInstances[1].ShouldBeSameAs(VoidInstances[2]);
    }

    [Fact]
    public async Task Given_Scoped_Typed_Value_Chain_When_Dispatching_Twice_Then_Every_Level_Is_Resolved_Again()
    {
        using IServiceScope scope = BuildResolutionScope(
            typeof(FirstStage), typeof(SecondStage));
        var counting = new CountingProvider(scope.ServiceProvider);
        var dispatcher = new ValueRequestDispatcher(
            scope.ServiceProvider.GetRequiredService<DispatchMap>(), counting);

        await dispatcher.SendAsync(new Resolved());
        counting.Requested.Clear();
        await dispatcher.SendAsync(new Resolved());

        counting.Requested.ShouldBe([
            typeof(FirstStage),
            typeof(SecondStage),
            typeof(IValueRequestHandler<Resolved, string>),
        ]);
    }

    [Fact]
    public async Task Given_Scoped_Direct_Void_Value_Chain_When_Dispatching_Twice_Then_Every_Level_Is_Resolved_Again()
    {
        using IServiceScope scope = BuildResolutionScope(
            typeof(FirstVoidStage), typeof(SecondVoidStage));
        var counting = new CountingProvider(scope.ServiceProvider);
        var dispatcher = new ValueRequestDispatcher(
            scope.ServiceProvider.GetRequiredService<DispatchMap>(), counting);

        await dispatcher.SendAsync(new ResolvedVoid());
        counting.Requested.Clear();
        await dispatcher.SendAsync(new ResolvedVoid());

        counting.Requested.ShouldBe([
            typeof(FirstVoidStage),
            typeof(SecondVoidStage),
            typeof(IValueRequestHandler<ResolvedVoid>),
        ]);
    }

    #region Initialization

    private static readonly List<object> TypedInstances = [];
    private static readonly List<object> VoidInstances = [];

    public ValueStageLifetimeTests()
    {
        TypedInstances.Clear();
        VoidInstances.Clear();
    }

    #endregion

    #region Helpers

    private static ServiceProvider BuildTypedProvider(Action<StageOptions>? lifetime = null)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options =>
        {
            options.AddHandler<TypedLifetimeHandler>();
            options.AddValueStage(typeof(TypedLifetimeStage), lifetime);
        });
        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildVoidProvider(Action<StageOptions>? lifetime = null)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options =>
        {
            options.AddHandler<VoidLifetimeHandler>();
            options.AddValueStage(typeof(VoidLifetimeStage), lifetime);
        });
        return services.BuildServiceProvider();
    }

    private static IServiceScope BuildResolutionScope(params Type[] stageTypes)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options =>
        {
            options.AddHandler<ResolvedHandler>();
            options.AddHandler<ResolvedVoidHandler>();
            options.WithScopedHandlers();
            foreach (Type stageType in stageTypes)
            {
                options.AddValueStage(stageType, stage => stage.AsScoped());
            }
        });

        return services.BuildServiceProvider().CreateScope();
    }

    private static async Task DispatchTypedThreeTimesAsync(ServiceProvider provider)
    {
        using (IServiceScope first = provider.CreateScope())
        {
            IValueRequestDispatcher dispatcher =
                first.ServiceProvider.GetRequiredService<IValueRequestDispatcher>();
            await dispatcher.SendAsync(new TypedLifetime());
            await dispatcher.SendAsync(new TypedLifetime());
        }

        using IServiceScope second = provider.CreateScope();
        await second.ServiceProvider.GetRequiredService<IValueRequestDispatcher>()
            .SendAsync(new TypedLifetime());
    }

    private static async Task DispatchVoidThreeTimesAsync(ServiceProvider provider)
    {
        using (IServiceScope first = provider.CreateScope())
        {
            IValueRequestDispatcher dispatcher =
                first.ServiceProvider.GetRequiredService<IValueRequestDispatcher>();
            await dispatcher.SendAsync(new VoidLifetime());
            await dispatcher.SendAsync(new VoidLifetime());
        }

        using IServiceScope second = provider.CreateScope();
        await second.ServiceProvider.GetRequiredService<IValueRequestDispatcher>()
            .SendAsync(new VoidLifetime());
    }

    private sealed record TypedLifetime : IValueRequest<string>;

    private sealed record VoidLifetime : IValueRequest;

    private sealed record Resolved : IValueRequest<string>;

    private sealed record ResolvedVoid : IValueRequest;

    private sealed class TypedLifetimeHandler : IValueRequestHandler<TypedLifetime, string>
    {
        public ValueTask<string> HandleAsync(
            TypedLifetime request,
            CancellationToken cancellationToken)
            => new("typed");
    }

    private sealed class VoidLifetimeHandler : IValueRequestHandler<VoidLifetime>
    {
        public ValueTask HandleAsync(
            VoidLifetime request,
            CancellationToken cancellationToken)
            => default;
    }

    private sealed class ResolvedHandler : IValueRequestHandler<Resolved, string>
    {
        public ValueTask<string> HandleAsync(
            Resolved request,
            CancellationToken cancellationToken)
            => new("resolved");
    }

    private sealed class ResolvedVoidHandler : IValueRequestHandler<ResolvedVoid>
    {
        public ValueTask HandleAsync(
            ResolvedVoid request,
            CancellationToken cancellationToken)
            => default;
    }

    private sealed class TypedLifetimeStage : IValueRequestStage<TypedLifetime, string>
    {
        public ValueTask<string> HandleAsync(
            TypedLifetime request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken)
        {
            TypedInstances.Add(this);
            return next.InvokeAsync(cancellationToken);
        }
    }

    private sealed class VoidLifetimeStage : IValueRequestStage<VoidLifetime>
    {
        public ValueTask HandleAsync(
            VoidLifetime request,
            ValueContinuation next,
            CancellationToken cancellationToken)
        {
            VoidInstances.Add(this);
            return next.InvokeAsync(cancellationToken);
        }
    }

    private sealed class FirstStage : IValueRequestStage<Resolved, string>
    {
        public ValueTask<string> HandleAsync(
            Resolved request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed class SecondStage : IValueRequestStage<Resolved, string>
    {
        public ValueTask<string> HandleAsync(
            Resolved request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed class FirstVoidStage : IValueRequestStage<ResolvedVoid>
    {
        public ValueTask HandleAsync(
            ResolvedVoid request,
            ValueContinuation next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed class SecondVoidStage : IValueRequestStage<ResolvedVoid>
    {
        public ValueTask HandleAsync(
            ResolvedVoid request,
            ValueContinuation next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    #endregion
}
