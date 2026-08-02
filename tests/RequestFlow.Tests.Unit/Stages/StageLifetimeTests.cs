using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class StageLifetimeTests
{
    [Fact]
    public async Task Given_Stage_With_A_Constructor_Dependency_When_Sending_Request_Then_It_Is_Injected()
    {
        ServiceProvider provider = Build();

        await SendAsync(provider);

        StageMarkers.Count.ShouldBe(1);
        StageMarkers[0].ShouldNotBeNull();
    }

    [Fact]
    public async Task Given_Scoped_Dependency_When_Sending_Request_Then_Stage_And_Handler_Share_The_Instance()
    {
        ServiceProvider provider = Build();

        await SendAsync(provider);

        StageMarkers[0].ShouldBeSameAs(HandlerMarkers[0]);
    }

    [Fact]
    public async Task Given_Scoped_Dependency_When_Sending_From_Two_Scopes_Then_Each_Scope_Gets_Its_Own_Instance()
    {
        ServiceProvider provider = Build();

        await SendTwiceInSeparateScopesAsync(provider);

        StageMarkers.Count.ShouldBe(2);
        StageMarkers[0].ShouldNotBeSameAs(StageMarkers[1]);
    }

    [Fact]
    public async Task Given_Transient_Stage_When_Sending_Twice_In_One_Scope_Then_A_New_Stage_Instance_Runs_Each_Time()
    {
        ServiceProvider provider = Build();

        await SendTwiceInOneScopeAsync(provider);

        StageInstances[0].ShouldNotBeSameAs(StageInstances[1]);
        StageMarkers[0].ShouldBeSameAs(StageMarkers[1]);
    }

    [Fact]
    public async Task Given_Stage_That_Skips_Next_When_Sending_Request_Then_The_Stage_Below_Is_Never_Constructed()
    {
        ServiceProvider provider = BuildTraceChain(typeof(SkipNextStage));

        await SendTraceAsync(provider);

        CountingStageConstructions.ShouldBe(0);
    }

    [Fact]
    public async Task Given_Stage_That_Calls_Next_Twice_When_Sending_Request_Then_The_Stage_Below_Is_Constructed_Once()
    {
        ServiceProvider provider = BuildTraceChain(typeof(DoubleNextStage));

        await SendTraceAsync(provider);

        CountingStageConstructions.ShouldBe(1);
    }

    [Fact]
    public async Task Given_Stage_That_Skips_Next_When_Sending_Request_Then_The_Handler_Is_Never_Constructed()
    {
        ServiceProvider provider = BuildTraceChain(typeof(SkipNextStage));

        await SendTraceAsync(provider);

        TraceHandlerConstructions.ShouldBe(0);
    }

    [Fact]
    public async Task Given_Stage_That_Calls_Next_Twice_When_Sending_Request_Then_The_Handler_Is_Constructed_Once()
    {
        ServiceProvider provider = BuildTraceChain(typeof(DoubleNextStage));

        await SendTraceAsync(provider);

        TraceHandlerConstructions.ShouldBe(1);
    }

    [Fact]
    public async Task Given_Void_Stage_That_Skips_Next_When_Sending_Request_Then_The_Handler_Is_Never_Constructed()
    {
        ServiceProvider provider = BuildVoidTraceChain(typeof(SkipNextVoidStage));

        await SendVoidTraceAsync(provider);

        VoidTraceHandlerConstructions.ShouldBe(0);
    }

    [Fact]
    public async Task Given_Void_Stage_That_Calls_Next_Twice_When_Sending_Request_Then_The_Handler_Is_Constructed_Once()
    {
        ServiceProvider provider = BuildVoidTraceChain(typeof(DoubleNextVoidStage));

        await SendVoidTraceAsync(provider);

        VoidTraceHandlerConstructions.ShouldBe(1);
    }

    // Resolving a level lazily puts the container's failure inside the chain, where the stages
    // above it can see it, instead of ahead of it where it escaped SendAsync untouched.
    [Fact]
    public async Task Given_Stage_That_Cannot_Be_Constructed_When_An_Outer_Stage_Wraps_It_Then_That_Stage_Observes_The_Failure()
    {
        ServiceProvider provider = BuildStageChain(typeof(CatchingStage), typeof(UnbuildableStage));

        string result = await SendTraceForResultAsync(provider);

        result.ShouldBe("caught");
    }

    [Fact]
    public async Task Given_Handler_That_Cannot_Be_Constructed_When_A_Stage_Wraps_It_Then_That_Stage_Observes_The_Failure()
    {
        ServiceProvider provider = BuildStageChain(typeof(CatchingUnbuildableStage));

        string result = await SendUnbuildableForResultAsync(provider);

        result.ShouldBe("caught");
    }

    [Fact]
    public void Given_Stage_With_No_Declared_Lifetime_When_Adding_Request_Flow_Then_The_Closed_Stage_Is_Transient()
    {
        ServiceCollection services = Collect(o => o.AddStage(typeof(ProbeStage<,>)));

        LifetimeOf(services).ShouldBe(ServiceLifetime.Transient);
    }

    [Fact]
    public void Given_Stage_Declared_Singleton_When_Adding_Request_Flow_Then_The_Closed_Stage_Is_Singleton()
    {
        ServiceCollection services = Collect(o => o.AddStage(typeof(ProbeStage<,>), s => s.AsSingleton()));

        LifetimeOf(services).ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void Given_Stage_Declared_Scoped_When_Adding_Request_Flow_Then_The_Closed_Stage_Is_Scoped()
    {
        ServiceCollection services = Collect(o => o.AddStage(typeof(ProbeStage<,>), s => s.AsScoped()));

        LifetimeOf(services).ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Given_Two_Stages_With_Different_Lifetimes_When_Adding_Request_Flow_Then_Each_Keeps_Its_Own()
    {
        ServiceCollection services = Collect(o => o
            .AddStage(typeof(ProbeStage<,>), s => s.AsSingleton())
            .AddStage(typeof(MarkerStage<,>), s => s.AsScoped()));

        LifetimeOf(services).ShouldBe(ServiceLifetime.Singleton);
        LifetimeOf(services, typeof(MarkerStage<Probe, string>)).ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public async Task Given_Singleton_Stage_When_Sending_From_Two_Scopes_Then_One_Instance_Runs_Both()
    {
        ServiceProvider provider = Collect(o => o.AddStage(typeof(ProbeStage<,>), s => s.AsSingleton()))
            .BuildServiceProvider();

        await SendTwiceInSeparateScopesAsync(provider);

        StageInstances[0].ShouldBeSameAs(StageInstances[1]);
    }

    [Fact]
    public async Task Given_Scoped_Stage_When_Sending_Twice_In_One_Scope_Then_One_Instance_Runs_Both()
    {
        ServiceProvider provider = Collect(o => o.AddStage(typeof(ProbeStage<,>), s => s.AsScoped()))
            .BuildServiceProvider();

        await SendTwiceInOneScopeAsync(provider);

        StageInstances[0].ShouldBeSameAs(StageInstances[1]);
    }

    [Fact]
    public async Task Given_Scoped_Stage_When_Sending_From_Two_Scopes_Then_Each_Scope_Gets_Its_Own_Instance()
    {
        ServiceProvider provider = Collect(o => o.AddStage(typeof(ProbeStage<,>), s => s.AsScoped()))
            .BuildServiceProvider();

        await SendTwiceInSeparateScopesAsync(provider);

        StageInstances[0].ShouldNotBeSameAs(StageInstances[1]);
    }

    // Stages register the way handlers do, so the declaration's descriptor comes last and wins,
    // and the one registered first is left behind for ValidateOnBuild to walk.
    [Fact]
    public void Given_Consumer_Registered_Stage_Before_Adding_Request_Flow_When_Adding_Request_Flow_Then_The_Declaration_Wins()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ProbeStage<Probe, string>>();

        AddFlow(services, o => o.AddStage(typeof(ProbeStage<,>), s => s.AsScoped()));

        List<ServiceDescriptor> descriptors = DescriptorsFor(services);
        descriptors.Count.ShouldBe(2);
        descriptors[1].Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public async Task Given_Stage_Registered_After_Adding_Request_Flow_When_Sending_Then_That_Registration_Wins()
    {
        ServiceCollection services = Collect(o => o.AddStage(typeof(ProbeStage<,>)));
        services.AddSingleton<ProbeStage<Probe, string>>();

        await SendTwiceInSeparateScopesAsync(services.BuildServiceProvider());

        StageInstances[0].ShouldBeSameAs(StageInstances[1]);
    }

    // One descriptor is the point: ValidateOnBuild walks every one of them, and the descriptor
    // AddStage leaves behind names a constructor the container may not be able to satisfy.
    [Fact]
    public void Given_Hand_Built_Stage_Replacing_The_Declaration_Then_Only_That_Descriptor_Remains()
    {
        ServiceCollection services = Collect(o => o.AddStage(typeof(ProbeStage<,>)));

        services.Replace(ServiceDescriptor.Singleton(new ProbeStage<Probe, string>()));

        List<ServiceDescriptor> descriptors = DescriptorsFor(services);
        descriptors.Count.ShouldBe(1);
        descriptors[0].ImplementationInstance.ShouldNotBeNull();
    }

    [Fact]
    public void Given_Stage_Declared_In_An_Earlier_Call_When_Adding_Request_Flow_Again_Then_It_Is_Registered_Once()
    {
        ServiceCollection services = Collect(o => o.AddStage(typeof(ProbeStage<,>), s => s.AsSingleton()));

        AddFlow(services,_ => { });

        DescriptorsFor(services).Count.ShouldBe(1);
    }

    #region Initialization

    // The container instantiates stages, so what they saw lands in statics; the constructor
    // clears them per test.
    private static readonly List<ScopeMarker> StageMarkers = [];
    private static readonly List<ScopeMarker> HandlerMarkers = [];
    private static readonly List<object> StageInstances = [];
    private static int CountingStageConstructions;
    private static int TraceHandlerConstructions;
    private static int VoidTraceHandlerConstructions;

    public StageLifetimeTests()
    {
        StageMarkers.Clear();
        HandlerMarkers.Clear();
        StageInstances.Clear();
        CountingStageConstructions = 0;
        TraceHandlerConstructions = 0;
        VoidTraceHandlerConstructions = 0;
    }

    #endregion

    #region Helpers

    private static ServiceProvider Build()
        => Collect(o => o.AddStage(typeof(MarkerStage<,>))).BuildServiceProvider();

    private static ServiceCollection Collect(Action<RequestFlowOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopeMarker>();
        AddFlow(services, configure);

        return services;
    }

    private static void AddFlow(ServiceCollection services, Action<RequestFlowOptions> configure)
        => services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StageLifetimeTests>();
            configure(o);
        });

    private static List<ServiceDescriptor> DescriptorsFor(
        IServiceCollection services, Type? stageType = null)
        => [.. services.Where(d => d.ServiceType == (stageType ?? typeof(ProbeStage<Probe, string>)))];

    private static ServiceLifetime LifetimeOf(IServiceCollection services, Type? stageType = null)
        => DescriptorsFor(services, stageType).Single().Lifetime;

    private static async Task SendAsync(ServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IRequestDispatcher>().SendAsync(new Probe());
    }

    private static async Task SendTwiceInSeparateScopesAsync(ServiceProvider provider)
    {
        await SendAsync(provider);
        await SendAsync(provider);
    }

    private static async Task SendTwiceInOneScopeAsync(ServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        await dispatcher.SendAsync(new Probe());
        await dispatcher.SendAsync(new Probe());
    }

    private static ServiceProvider BuildStageChain(params Type[] stageTypes)
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopeMarker>();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StageLifetimeTests>();
            foreach (Type stageType in stageTypes)
            {
                o.AddStage(stageType);
            }
        });

        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildTraceChain(Type outerStageType)
        => BuildStageChain(outerStageType, typeof(CountingStage));

    private static async Task SendTraceAsync(ServiceProvider provider)
        => await SendTraceForResultAsync(provider);

    private static async Task<string> SendTraceForResultAsync(ServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IRequestDispatcher>().SendAsync(new Trace());
    }

    private static async Task<string> SendUnbuildableForResultAsync(ServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IRequestDispatcher>().SendAsync(new Unbuildable());
    }

    private static ServiceProvider BuildVoidTraceChain(Type stageType) => BuildStageChain(stageType);

    private static async Task SendVoidTraceAsync(ServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IRequestDispatcher>().SendAsync(new VoidTrace());
    }

    public sealed class ScopeMarker
    { }

    public sealed record Probe : IRequest<string>;

    public sealed class ProbeHandler(ScopeMarker marker) : IRequestHandler<Probe, string>
    {
        public Task<string> HandleAsync(Probe request, CancellationToken cancellationToken)
        {
            HandlerMarkers.Add(marker);
            return Task.FromResult("probe");
        }
    }

    public sealed class MarkerStage<TRequest, TResponse>(ScopeMarker marker) : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request, IContinuation<TResponse> next, CancellationToken cancellationToken)
        {
            StageMarkers.Add(marker);
            StageInstances.Add(this);

            return next.InvokeAsync();
        }
    }

    // No constructor dependencies, so it registers cleanly under any lifetime.
    public sealed class ProbeStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request, IContinuation<TResponse> next, CancellationToken cancellationToken)
        {
            StageInstances.Add(this);

            return next.InvokeAsync();
        }
    }

    public sealed record Trace : IRequest<string>;

    public sealed class TraceHandler : IRequestHandler<Trace, string>
    {
        public TraceHandler()
            => TraceHandlerConstructions++;

        public Task<string> HandleAsync(Trace request, CancellationToken cancellationToken)
            => Task.FromResult("traced");
    }

    public sealed record VoidTrace : IRequest;

    public sealed class VoidTraceHandler : IRequestHandler<VoidTrace>
    {
        public VoidTraceHandler()
            => VoidTraceHandlerConstructions++;

        public Task HandleAsync(VoidTrace request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class SkipNextVoidStage : IRequestStage<VoidTrace>
    {
        public Task HandleAsync(VoidTrace request, IContinuation next, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class DoubleNextVoidStage : IRequestStage<VoidTrace>
    {
        public async Task HandleAsync(VoidTrace request, IContinuation next, CancellationToken cancellationToken)
        {
            await next.InvokeAsync();
            await next.InvokeAsync();
        }
    }

    public sealed class SkipNextStage : IRequestStage<Trace, string>
    {
        public Task<string> HandleAsync(Trace request, IContinuation<string> next, CancellationToken cancellationToken)
            => Task.FromResult("short-circuited");
    }

    public sealed class DoubleNextStage : IRequestStage<Trace, string>
    {
        public async Task<string> HandleAsync(
            Trace request, IContinuation<string> next, CancellationToken cancellationToken)
        {
            await next.InvokeAsync();
            return await next.InvokeAsync();
        }
    }

    public sealed class CountingStage : IRequestStage<Trace, string>
    {
        public CountingStage()
            => CountingStageConstructions++;

        public Task<string> HandleAsync(Trace request, IContinuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    // Nothing registers this, so any level that asks the container for it fails to build.
    public sealed class MissingDependency
    { }

    public sealed class UnbuildableStage : IRequestStage<Trace, string>
    {
        public UnbuildableStage(MissingDependency dependency)
        { }

        public Task<string> HandleAsync(Trace request, IContinuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    public sealed class CatchingStage : IRequestStage<Trace, string>
    {
        public async Task<string> HandleAsync(
            Trace request, IContinuation<string> next, CancellationToken cancellationToken)
        {
            try
            {
                return await next.InvokeAsync();
            }
            catch (InvalidOperationException)
            {
                return "caught";
            }
        }
    }

    public sealed record Unbuildable : IRequest<string>;

    public sealed class UnbuildableHandler : IRequestHandler<Unbuildable, string>
    {
        public UnbuildableHandler(MissingDependency dependency)
        { }

        public Task<string> HandleAsync(Unbuildable request, CancellationToken cancellationToken)
            => Task.FromResult("unreachable");
    }

    public sealed class CatchingUnbuildableStage : IRequestStage<Unbuildable, string>
    {
        public async Task<string> HandleAsync(
            Unbuildable request, IContinuation<string> next, CancellationToken cancellationToken)
        {
            try
            {
                return await next.InvokeAsync();
            }
            catch (InvalidOperationException)
            {
                return "caught";
            }
        }
    }

    #endregion
}
