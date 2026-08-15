using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    public async Task Given_Registered_Cqrs_When_Streaming_Query_Then_Yields_Query_Handler_Items()
    {
        List<string> items = [];

        await foreach (string item in _streamQueries.Stream(new ListOrders()))
        {
            items.Add(item);
        }

        items.ShouldBe(["order 1", "order 2"]);
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
    private readonly IStreamQueryDispatcher _streamQueries;

    public AddCqrsTests()
    {
        var services = new ServiceCollection();
        services
            .AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddCqrsTests>())
            .AddCqrs();
        _provider = services.BuildServiceProvider();
        _commands = _provider.GetRequiredService<ICommandDispatcher>();
        _queries = _provider.GetRequiredService<IQueryDispatcher>();
        _streamQueries = _provider.GetRequiredService<IStreamQueryDispatcher>();
    }

    public void Dispose()
        => _provider.Dispose();

    #endregion

    #region Helpers

    public sealed record CreateOrder(string Item) : ICommand<string>;

    public sealed record CancelOrder : ICommand;

    public sealed record GetOrder(string Id) : IQuery<string>;

    public sealed record ListOrders : IStreamQuery<string>;

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

    public sealed class ListOrdersHandler : IStreamQueryHandler<ListOrders, string>
    {
        public async IAsyncEnumerable<string> Handle(
            ListOrders request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return "order 1";
            yield return "order 2";
        }
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
        services.ShouldContain(d =>
            d.ServiceType == typeof(IStreamQueryDispatcher) && d.Lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public void Given_Cqrs_Registered_Twice_When_Registering_Then_Dispatchers_Are_Registered_Once()
    {
        var services = new ServiceCollection();
        RequestFlowBuilder builder = RegisterRequestFlow(services);

        builder.AddCqrs().AddCqrs();

        services.Count(d => d.ServiceType == typeof(ICommandDispatcher)).ShouldBe(1);
        services.Count(d => d.ServiceType == typeof(IQueryDispatcher)).ShouldBe(1);
        services.Count(d => d.ServiceType == typeof(IStreamQueryDispatcher)).ShouldBe(1);
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

    [Fact]
    public void Given_Add_Cqrs_When_Registering_Then_The_Split_Rule_Is_Registered_Once()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddCqrsTests>())
            .AddCqrs()
            .AddCqrs();

        services.Count(d =>
            d.ServiceType == typeof(IRequestFlowValidationRule)
            && d.ImplementationType == typeof(CommandQuerySplitRule)).ShouldBe(1);
    }

    [Fact]
    public void Given_A_Confused_Request_When_Validating_With_Cqrs_Then_Throws_With_The_Split_Problem()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
            {
                o.RegisterHandlersFromAssembly(typeof(RequestFlow.Tests.ValidationFixtures.Confused).Assembly);
                o.AllowUnhandledRequests();
            })
            .AddCqrs();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        exception.Problems.ShouldContain(p =>
            p.Code == "CQRS0001" && p.Subject == typeof(RequestFlow.Tests.ValidationFixtures.Confused));
    }

    // A request mixing IQuery with IStreamQuery would fail this assembly's freeze in every test,
    // so the fixtures assembly's MixedCqrsFamilies carries the two families through the CQRS
    // contracts alone.
    [Fact]
    public void Given_A_Mixed_Cqrs_Families_Request_When_Validating_With_Cqrs_Then_Throws_With_The_Request_And_Stream_Problem()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
            {
                o.RegisterHandlersFromAssembly(typeof(RequestFlow.Tests.ValidationFixtures.MixedCqrsFamilies).Assembly);
                o.AllowUnhandledRequests();
            })
            .AddCqrs();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        exception.Problems.ShouldContain(p =>
            p.Code == ProblemCodes.RequestAndStreamRequest
            && p.Subject == typeof(RequestFlow.Tests.ValidationFixtures.MixedCqrsFamilies));
    }

    // The base contracts collide too, so the freeze reports the shape under both codes.
    [Fact]
    public void Given_A_Stream_Confused_Request_When_Validating_With_Cqrs_Then_Throws_With_Both_Problems()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
            {
                o.RegisterHandlersFromAssembly(typeof(RequestFlow.Tests.ValidationFixtures.StreamConfused).Assembly);
                o.AllowUnhandledRequests();
            })
            .AddCqrs();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        exception.Problems.ShouldContain(p =>
            p.Code == ProblemCodes.RequestAndStreamRequest
            && p.Subject == typeof(RequestFlow.Tests.ValidationFixtures.StreamConfused));
        exception.Problems.ShouldContain(p =>
            p.Code == CqrsProblemCodes.CommandQuerySplit
            && p.Subject == typeof(RequestFlow.Tests.ValidationFixtures.StreamConfused));
    }

    [Fact]
    public void Given_Cqrs_Handlers_When_Validating_Then_A_Rule_Sees_The_Command_Contracts()
    {
        var rule = new CapturingRule();
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddCqrsTests>()).AddCqrs();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRequestFlowValidationRule>(rule));
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.ValidateRequestFlow();

        ContractOf(rule, typeof(AddCqrsTests.CreateOrder)).ShouldBe(typeof(ICommandHandler<,>));
        ContractOf(rule, typeof(AddCqrsTests.CancelOrder)).ShouldBe(typeof(ICommandHandler<>));
        ContractOf(rule, typeof(AddCqrsTests.GetOrder)).ShouldBe(typeof(IQueryHandler<,>));
        ContractOf(rule, typeof(AddCqrsTests.ListOrders)).ShouldBe(typeof(IStreamQueryHandler<,>));
    }

    #region Helpers

    private static RequestFlowBuilder RegisterRequestFlow(IServiceCollection services)
        => services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddCqrsTests>());

    private static Type ContractOf(CapturingRule rule, Type requestType)
        => rule.Model!.Requests.Single(r => r.RequestType == requestType).Handlers.Single().ContractType;

    private sealed class CapturingRule : IRequestFlowValidationRule
    {
        public RequestFlowModel? Model { get; private set; }

        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
        {
            Model = context.Model;

            return [];
        }
    }

    #endregion
}
