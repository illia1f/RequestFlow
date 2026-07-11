using Microsoft.Extensions.DependencyInjection;
using RequestFlow;
using RequestFlow.Tests.ValidationFixtures;

namespace RequestFlow.Tests.Unit;

public sealed class AddRequestFlowTests : IDisposable
{
    [Fact]
    public async Task Given_Registered_Request_Flow_When_Sending_Request_Then_Returns_Handler_Response()
    {
        string result = await _sut.SendAsync(new Echo("hello"));

        result.ShouldBe("hello");
    }

    [Fact]
    public async Task Given_Registered_Request_Flow_When_Sending_Void_Request_Then_Void_Handler_Is_Invoked()
    {
        int before = DropHandler.Calls;

        await _sut.SendAsync(new Drop());

        DropHandler.Calls.ShouldBe(before + 1);
    }

    [Fact]
    public void Given_Registered_Request_Flow_When_Resolving_Dispatcher_In_Scope_Then_Dispatcher_Is_Resolved()
    {
        using IServiceScope scope = _provider.CreateScope();

        var dispatcher = scope.ServiceProvider.GetService<IRequestDispatcher>();

        dispatcher.ShouldNotBeNull();
    }

    #region Initialization

    private readonly ServiceProvider _provider;
    private readonly IRequestDispatcher _sut;

    public AddRequestFlowTests()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddRequestFlowTests>());
        _provider = services.BuildServiceProvider();
        _sut = _provider.GetRequiredService<IRequestDispatcher>();
    }

    public void Dispose()
        => _provider.Dispose();

    #endregion

    #region Helpers

    public sealed record Echo(string Text) : IRequest<string>;

    public sealed record Drop : IRequest;

    public sealed class EchoHandler : IRequestHandler<Echo, string>
    {
        public Task<string> HandleAsync(Echo request, CancellationToken cancellationToken)
            => Task.FromResult(request.Text);
    }

    public sealed class DropHandler : IRequestHandler<Drop>
    {
        public static int Calls;

        public Task HandleAsync(Drop request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    #endregion
}

public sealed class AddRequestFlowValidationTests
{
    [Fact]
    public void Given_Void_Handler_When_Registering_Request_Flow_Then_Handler_Is_Registered_Under_Standalone_Interface_Only()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddRequestFlowTests>());

        services.ShouldContain(d => d.ServiceType == typeof(IRequestHandler<AddRequestFlowTests.Drop>));
        services.ShouldNotContain(d => d.ServiceType == typeof(IRequestHandler<AddRequestFlowTests.Drop, NoResult>));
    }

    [Fact]
    public void Given_Request_Without_Handler_When_Resolving_Dispatcher_Then_Validation_Reports_Missing_Handler()
    {
        RequestFlowValidationException exception = ScanFixtureAssembly();

        exception.Problems.ShouldContain(p => p.Contains(nameof(Lonely)));
    }

    [Fact]
    public void Given_Request_With_Duplicate_Handlers_When_Resolving_Dispatcher_Then_Validation_Reports_Duplicate_Handlers()
    {
        RequestFlowValidationException exception = ScanFixtureAssembly();

        exception.Problems.ShouldContain(p => p.Contains(nameof(Duplicated)));
    }

    [Fact]
    public void Given_Derived_Request_Without_Own_Handler_When_Resolving_Dispatcher_Then_Validation_Reports_Missing_Handler()
    {
        RequestFlowValidationException exception = ScanFixtureAssembly();

        exception.Problems.ShouldContain(p => p.Contains(nameof(Orphaned)));
    }

    [Fact]
    public void Given_Keyed_Service_Registered_First_When_Registering_Request_Flow_Then_Registration_Succeeds()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<object>("cache", new object());

        Should.NotThrow(() =>
            services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddRequestFlowTests>()));
    }

    [Fact]
    public void Given_Unhandled_Requests_Allowed_When_Resolving_Dispatcher_Then_Missing_Handler_Is_Not_Reported()
    {
        RequestFlowValidationException exception = ScanFixtureAssembly(allowUnhandledRequests: true);

        exception.Problems.ShouldNotContain(p => p.Contains(nameof(Lonely)));
    }

    [Fact]
    public void Given_Unhandled_Requests_Allowed_When_Resolving_Dispatcher_Then_Duplicate_Handlers_Are_Still_Reported()
    {
        RequestFlowValidationException exception = ScanFixtureAssembly(allowUnhandledRequests: true);

        exception.Problems.ShouldContain(p => p.Contains(nameof(Duplicated)));
    }

    [Fact]
    public void Given_Unhandled_Requests_Allowed_In_Second_Call_When_Resolving_Dispatcher_Then_Missing_Handler_Is_Not_Reported()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssembly(typeof(Lonely).Assembly));
        services.AddRequestFlow(o => o.AllowUnhandledRequests());

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        exception.Problems.ShouldNotContain(p => p.Contains(nameof(Lonely)));
    }

    [Fact]
    public void Given_Scoped_Handler_Lifetime_When_Registering_Request_Flow_Then_Handlers_Are_Registered_Scoped()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<AddRequestFlowTests>()
            .WithHandlerLifetime(ServiceLifetime.Scoped));

        ServiceDescriptor descriptor = services
            .Single(d => d.ServiceType == typeof(IRequestHandler<AddRequestFlowTests.Echo, string>));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Given_Default_Options_When_Registering_Request_Flow_Then_Dispatcher_Is_Registered_Scoped()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddRequestFlowTests>());

        ServiceDescriptor descriptor = services.Single(d => d.ServiceType == typeof(IRequestDispatcher));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Given_Transient_Dispatcher_When_Registering_Request_Flow_Then_Dispatcher_Is_Registered_Transient()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<AddRequestFlowTests>()
            .WithTransientDispatcher());

        ServiceDescriptor descriptor = services.Single(d => d.ServiceType == typeof(IRequestDispatcher));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);
    }

    [Fact]
    public void Given_Transient_Dispatcher_In_Second_Call_When_Registering_Request_Flow_Then_First_Call_Lifetime_Wins()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddRequestFlowTests>());
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<AddRequestFlowTests>()
            .WithTransientDispatcher());

        ServiceDescriptor descriptor = services.Single(d => d.ServiceType == typeof(IRequestDispatcher));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public async Task Given_Same_Assembly_Registered_In_Two_Calls_When_Sending_Request_Then_Handler_Handles_Request()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddRequestFlowTests>());
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddRequestFlowTests>());
        using ServiceProvider provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IRequestDispatcher>();

        string result = await dispatcher.SendAsync(new AddRequestFlowTests.Echo("twice"));

        result.ShouldBe("twice");
    }

    [Fact]
    public async Task Given_Two_Providers_Built_From_One_Collection_When_Sending_Request_Then_Both_Providers_Dispatch()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddRequestFlowTests>());
        using ServiceProvider first = services.BuildServiceProvider();
        using ServiceProvider second = services.BuildServiceProvider();
        await first.GetRequiredService<IRequestDispatcher>().SendAsync(new AddRequestFlowTests.Echo("first"));

        string result = await second.GetRequiredService<IRequestDispatcher>().SendAsync(new AddRequestFlowTests.Echo("second"));

        result.ShouldBe("second");
    }

    [Fact]
    public void Given_Two_Providers_Built_From_One_Collection_When_Validation_Fails_Then_Both_Providers_Report_Problems()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssembly(typeof(Lonely).Assembly));
        using ServiceProvider first = services.BuildServiceProvider();
        using ServiceProvider second = services.BuildServiceProvider();
        Should.Throw<RequestFlowValidationException>(() => first.GetRequiredService<IRequestDispatcher>());

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            second.GetRequiredService<IRequestDispatcher>());

        exception.Problems.ShouldContain(p => p.Contains(nameof(Lonely)));
    }

    #region Helpers

    private static RequestFlowValidationException ScanFixtureAssembly(bool allowUnhandledRequests = false)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssembly(typeof(Lonely).Assembly);
            if (allowUnhandledRequests)
                o.AllowUnhandledRequests();
        });

        return Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());
    }

    #endregion
}
