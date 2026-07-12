using Microsoft.Extensions.DependencyInjection;
using RequestFlow;
using RequestFlow.Cqrs;

namespace RequestFlow.Cqrs.Tests.Unit;

public sealed class AddCqrsTests : IDisposable
{
    [Fact]
    public async Task Given_Registered_Cqrs_When_Sending_Typed_Command_Then_Returns_Command_Handler_Response()
    {
        string result = await _commands.SendAsync(new CreateOrder("book"));

        result.ShouldBe("created book");
    }

    [Fact]
    public async Task Given_Registered_Cqrs_When_Sending_Void_Command_Then_Command_Handler_Is_Invoked()
    {
        int before = CancelOrderHandler.Calls;

        await _commands.SendAsync(new CancelOrder());

        CancelOrderHandler.Calls.ShouldBe(before + 1);
    }

    [Fact]
    public async Task Given_Registered_Cqrs_When_Sending_Query_Then_Returns_Query_Handler_Response()
    {
        string result = await _queries.SendAsync(new GetOrder("42"));

        result.ShouldBe("order 42");
    }

    [Fact]
    public async Task Given_Registered_Cqrs_When_Sending_Command_Through_Request_Dispatcher_Then_Returns_Handler_Response()
    {
        var dispatcher = _provider.GetRequiredService<IRequestDispatcher>();

        string result = await dispatcher.SendAsync(new CreateOrder("pen"));

        result.ShouldBe("created pen");
    }

    #region Initialization

    private readonly ServiceProvider _provider;
    private readonly ICommandDispatcher _commands;
    private readonly IQueryDispatcher _queries;

    public AddCqrsTests()
    {
        var services = new ServiceCollection();
        services
            .AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddCqrsTests>())
            .AddCqrs();
        _provider = services.BuildServiceProvider();
        _commands = _provider.GetRequiredService<ICommandDispatcher>();
        _queries = _provider.GetRequiredService<IQueryDispatcher>();
    }

    public void Dispose()
        => _provider.Dispose();

    #endregion

    #region Helpers

    public sealed record CreateOrder(string Item) : ICommand<string>;

    public sealed record CancelOrder : ICommand;

    public sealed record GetOrder(string Id) : IQuery<string>;

    public sealed class CreateOrderHandler : ICommandHandler<CreateOrder, string>
    {
        public Task<string> HandleAsync(CreateOrder request, CancellationToken cancellationToken)
            => Task.FromResult($"created {request.Item}");
    }

    public sealed class CancelOrderHandler : ICommandHandler<CancelOrder>
    {
        public static int Calls;

        public Task HandleAsync(CancelOrder request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    public sealed class GetOrderHandler : IQueryHandler<GetOrder, string>
    {
        public Task<string> HandleAsync(GetOrder request, CancellationToken cancellationToken)
            => Task.FromResult($"order {request.Id}");
    }

    #endregion
}

public sealed class AddCqrsRegistrationTests
{
    [Fact]
    public void Given_Cqrs_Handlers_When_Registering_Request_Flow_Then_Handlers_Are_Registered_Under_Core_Interfaces()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddCqrsTests>());

        services.ShouldContain(d =>
            d.ServiceType == typeof(IRequestHandler<AddCqrsTests.CreateOrder, string>));
        services.ShouldContain(d =>
            d.ServiceType == typeof(IRequestHandler<AddCqrsTests.CancelOrder>));
        services.ShouldContain(d =>
            d.ServiceType == typeof(IRequestHandler<AddCqrsTests.GetOrder, string>));
    }

    [Fact]
    public void Given_Registered_Request_Flow_When_Registering_Cqrs_Then_Dispatchers_Are_Registered_Transient()
    {
        var services = new ServiceCollection();
        RequestFlowBuilder builder = RegisterRequestFlow(services);

        builder.AddCqrs();

        services.ShouldContain(d =>
            d.ServiceType == typeof(ICommandDispatcher) && d.Lifetime == ServiceLifetime.Transient);
        services.ShouldContain(d =>
            d.ServiceType == typeof(IQueryDispatcher) && d.Lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public void Given_Cqrs_Registered_Twice_When_Registering_Then_Dispatchers_Are_Registered_Once()
    {
        var services = new ServiceCollection();
        RequestFlowBuilder builder = RegisterRequestFlow(services);

        builder.AddCqrs().AddCqrs();

        services.Count(d => d.ServiceType == typeof(ICommandDispatcher)).ShouldBe(1);
        services.Count(d => d.ServiceType == typeof(IQueryDispatcher)).ShouldBe(1);
    }

    [Fact]
    public void Given_Registered_Request_Flow_When_Registering_Cqrs_Then_Returns_Same_Builder()
    {
        RequestFlowBuilder builder = RegisterRequestFlow(new ServiceCollection());

        RequestFlowBuilder result = builder.AddCqrs();

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void Given_Null_Builder_When_Registering_Cqrs_Then_Throws_Argument_Null_Exception()
    {
        Should.Throw<ArgumentNullException>(
            () => ((RequestFlowBuilder)null!).AddCqrs());
    }

    #region Helpers

    private static RequestFlowBuilder RegisterRequestFlow(IServiceCollection services)
        => services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddCqrsTests>());

    #endregion
}
