using Microsoft.Extensions.DependencyInjection;
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
    public void Given_Consumer_Registered_Stage_When_Adding_Request_Flow_Then_The_Consumer_Registration_Is_Kept()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopeMarker>();
        services.AddSingleton<MarkerStage<Probe, string>>();

        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<StageLifetimeTests>()
            .AddStage(typeof(MarkerStage<,>)));

        List<ServiceDescriptor> descriptors =
            [.. services.Where(d => d.ServiceType == typeof(MarkerStage<Probe, string>))];
        descriptors.Count.ShouldBe(1);
        descriptors[0].Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    #region Initialization

    // The container instantiates stages, so what they saw lands in statics; the constructor
    // clears them per test.
    private static readonly List<ScopeMarker> StageMarkers = [];
    private static readonly List<ScopeMarker> HandlerMarkers = [];
    private static readonly List<object> StageInstances = [];

    public StageLifetimeTests()
    {
        StageMarkers.Clear();
        HandlerMarkers.Clear();
        StageInstances.Clear();
    }

    #endregion

    #region Helpers

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopeMarker>();
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<StageLifetimeTests>()
            .AddStage(typeof(MarkerStage<,>)));

        return services.BuildServiceProvider();
    }

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
            TRequest request, StageDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            StageMarkers.Add(marker);
            StageInstances.Add(this);

            return next();
        }
    }

    #endregion
}
