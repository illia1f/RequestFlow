using Microsoft.Extensions.DependencyInjection;
using RequestFlow;
using RequestFlow.Tests.ValidationFixtures;

namespace RequestFlow.Tests.Unit;

public sealed class RegisterGenericHandlerTests
{
    [Fact]
    public async Task Given_Declared_Closings_When_Sending_First_Closing_Request_Then_Generic_Handler_Handles_Request()
    {
        IRequestDispatcher dispatcher = Build(o =>
            o.RegisterGenericHandler(typeof(AuditHandler<>), typeof(Order), typeof(User)));

        string result = await dispatcher.SendAsync(new AuditRequest<Order>("o1"));

        result.ShouldBe("Order:o1");
    }

    [Fact]
    public async Task Given_Declared_Closings_When_Sending_Second_Closing_Request_Then_Generic_Handler_Handles_Request()
    {
        IRequestDispatcher dispatcher = Build(o =>
            o.RegisterGenericHandler(typeof(AuditHandler<>), typeof(Order), typeof(User)));

        string result = await dispatcher.SendAsync(new AuditRequest<User>("u1"));

        result.ShouldBe("User:u1");
    }

    [Fact]
    public async Task Given_Void_Generic_Handler_Closing_When_Sending_Request_Then_Handler_Is_Invoked()
    {
        IRequestDispatcher dispatcher = Build(o =>
            o.RegisterGenericHandler(typeof(NotifyHandler<>), typeof(Order)));
        int before = NotifyHandler<Order>.Calls;

        await dispatcher.SendAsync(new Notify<Order>());

        NotifyHandler<Order>.Calls.ShouldBe(before + 1);
    }

    [Fact]
    public async Task Given_Undeclared_Closing_When_Sending_Request_Then_Throws_Handler_Not_Found_Exception()
    {
        IRequestDispatcher dispatcher = Build(o =>
            o.RegisterGenericHandler(typeof(AuditHandler<>), typeof(Order)));

        await Should.ThrowAsync<HandlerNotFoundException>(
            () => dispatcher.SendAsync(new AuditRequest<Plain>("x")));
    }

    [Fact]
    public async Task Given_Repeated_Closing_When_Sending_Request_Then_Generic_Handler_Handles_Request()
    {
        IRequestDispatcher dispatcher = Build(o =>
            o.RegisterGenericHandler(typeof(AuditHandler<>), typeof(Order), typeof(Order)));

        string result = await dispatcher.SendAsync(new AuditRequest<Order>("x"));

        result.ShouldBe("Order:x");
    }

    [Fact]
    public void Given_Closing_Violating_Handler_Constraints_When_Resolving_Dispatcher_Then_Validation_Reports_Closing()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<RegisterGenericHandlerTests>();
            o.RegisterGenericHandler(typeof(ConstrainedHandler<>), typeof(Plain));
        });

        var exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        exception.Problems.ShouldContain(p => p.Contains("ConstrainedHandler") && p.Contains(nameof(Plain)));
    }

    [Fact]
    public void Given_Closing_Duplicating_Concrete_Handler_When_Resolving_Dispatcher_Then_Validation_Reports_Duplicate()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<RegisterGenericHandlerTests>();
            o.RegisterGenericHandler(typeof(AuditHandler<>), typeof(Dup));
        });

        var exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        exception.Problems.ShouldContain(p => p.Contains("more than one handler"));
    }

    [Fact]
    public async Task Given_Closing_Declared_In_Second_Registration_Call_When_Sending_Request_Then_Generic_Handler_Handles_Request()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<RegisterGenericHandlerTests>());
        services.AddRequestFlow(o => o.RegisterGenericHandler(typeof(AuditHandler<>), typeof(Order)));
        var dispatcher = services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>();

        string result = await dispatcher.SendAsync(new AuditRequest<Order>("o1"));

        result.ShouldBe("Order:o1");
    }

    [Fact]
    public void Given_Null_Handler_Type_When_Registering_Generic_Handler_Then_Throws_Argument_Null_Exception()
    {
        Action action = () => _options.RegisterGenericHandler(null!, typeof(Order));

        action.ShouldThrow<ArgumentNullException>();
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(AuditHandler<Order>))]
    [InlineData(typeof(TwoParamHandler<,>))]
    [InlineData(typeof(List<>))]
    [InlineData(typeof(AbstractAuditHandler<>))]
    public void Given_Invalid_Handler_Type_When_Resolving_Dispatcher_Then_Validation_Reports_Declaration(Type handlerType)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterGenericHandler(handlerType, typeof(Order)));

        var exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        exception.Problems.ShouldContain(p => p.Contains(handlerType.Name.Split('`')[0]));
    }

    [Fact]
    public void Given_Null_Closing_Array_When_Registering_Generic_Handler_Then_Throws_Argument_Null_Exception()
    {
        Action action = () => _options.RegisterGenericHandler(typeof(AuditHandler<>), null!);

        action.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Given_Empty_Closing_Array_When_Resolving_Dispatcher_Then_Validation_Reports_Declaration()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterGenericHandler(typeof(AuditHandler<>)));

        var exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        exception.Problems.ShouldContain(p => p.Contains("no closing types"));
    }

    [Fact]
    public void Given_Null_Closing_Element_When_Registering_Generic_Handler_Then_Throws_Argument_Exception()
    {
        Action action = () => _options.RegisterGenericHandler(typeof(AuditHandler<>), typeof(Order), null!);

        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Given_Open_Closing_Type_When_Resolving_Dispatcher_Then_Validation_Reports_Declaration()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterGenericHandler(typeof(AuditHandler<>), typeof(Order), typeof(List<>)));

        var exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        exception.Problems.ShouldContain(p => p.Contains("is not a closed type"));
    }

    [Fact]
    public void Given_Shape_Constraint_And_Missing_Handler_Problems_When_Resolving_Dispatcher_Then_Single_Exception_Lists_All_Problems()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssembly(typeof(Lonely).Assembly);
            o.RegisterGenericHandler(typeof(string), typeof(Order));
            o.RegisterGenericHandler(typeof(ConstrainedHandler<>), typeof(Plain));
        });

        var exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        exception.Problems.ShouldContain(p => p.Contains("System.String"));
        exception.Problems.ShouldContain(p => p.Contains("ConstrainedHandler") && p.Contains(nameof(Plain)));
        exception.Problems.ShouldContain(p => p.Contains(nameof(Lonely)));
    }

    [Fact]
    public void Given_Same_Invalid_Declaration_In_Two_Calls_When_Resolving_Dispatcher_Then_Validation_Reports_Problem_Once()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterGenericHandler(typeof(string), typeof(Order)));
        services.AddRequestFlow(o => o.RegisterGenericHandler(typeof(string), typeof(Order)));

        var exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        exception.Problems.Count(p => p.Contains("System.String")).ShouldBe(1);
    }

    #region Initialization

    private readonly RequestFlowOptions _options;

    public RegisterGenericHandlerTests()
    {
        _options = new RequestFlowOptions();
    }

    #endregion

    #region Helpers

    private static IRequestDispatcher Build(Action<RequestFlowOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<RegisterGenericHandlerTests>();
            configure(o);
        });
        return services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>();
    }

    public sealed record AuditRequest<T>(string Payload) : IRequest<string>;

    public sealed class AuditHandler<T> : IRequestHandler<AuditRequest<T>, string>
    {
        public Task<string> HandleAsync(AuditRequest<T> request, CancellationToken cancellationToken)
            => Task.FromResult($"{typeof(T).Name}:{request.Payload}");
    }

    public abstract class AbstractAuditHandler<T> : IRequestHandler<AuditRequest<T>, string>
    {
        public abstract Task<string> HandleAsync(AuditRequest<T> request, CancellationToken cancellationToken);
    }

    public sealed record Notify<T> : IRequest;

    public sealed class NotifyHandler<T> : IRequestHandler<Notify<T>>
    {
        public static int Calls;

        public Task HandleAsync(Notify<T> request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    public interface IAuditable
    { }

    public sealed class Order
    { }

    public sealed class User
    { }

    public sealed class Plain
    { }

    public sealed record Constrained<T>(string Payload) : IRequest<string>;

    public sealed class ConstrainedHandler<T> : IRequestHandler<Constrained<T>, string>
        where T : IAuditable
    {
        public Task<string> HandleAsync(Constrained<T> request, CancellationToken cancellationToken)
            => Task.FromResult(request.Payload);
    }

    public sealed class Dup
    { }

    public sealed class DupAuditHandler : IRequestHandler<AuditRequest<Dup>, string>
    {
        public Task<string> HandleAsync(AuditRequest<Dup> request, CancellationToken cancellationToken)
            => Task.FromResult("concrete");
    }

    public sealed class TwoParamHandler<T1, T2> : IRequestHandler<AuditRequest<T1>, string>
    {
        public Task<string> HandleAsync(AuditRequest<T1> request, CancellationToken cancellationToken)
            => Task.FromResult(request.Payload);
    }

    #endregion
}
