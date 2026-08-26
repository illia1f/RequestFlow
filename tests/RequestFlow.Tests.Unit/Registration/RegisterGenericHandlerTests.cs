using System.Reflection;
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

        exception.Problems.ShouldContain(p =>
            p.Code == "RF0007" && p.Subject == typeof(ConstrainedHandler<>)
            && p.Message.Contains("ConstrainedHandler") && p.Message.Contains(nameof(Plain)));
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

        exception.Problems.ShouldContain(p => p.Message.Contains("more than one handler"));
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
    [InlineData(typeof(string), "RF0001")]
    [InlineData(typeof(AuditHandler<Order>), "RF0001")]
    [InlineData(typeof(AbstractAuditHandler<>), "RF0002")]
    [InlineData(typeof(TwoParamHandler<,>), "RF0003")]
    [InlineData(typeof(List<>), "RF0004")]
    public void Given_Invalid_Handler_Type_When_Resolving_Dispatcher_Then_Validation_Reports_Declaration(
        Type handlerType, string expectedCode)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterGenericHandler(handlerType, typeof(Order)));

        var exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        string handlerName = handlerType.Name.Split('`')[0];
        exception.Problems.ShouldContain(p =>
            p.Code == expectedCode && p.Subject == handlerType && p.Message.Contains(handlerName));
    }

    [Fact]
    public void Given_A_Definition_Whose_Interfaces_Cannot_Load_When_Resolving_Dispatcher_Then_Validation_Reports_Declaration()
    {
        var handlerType = new UnloadableInterfacesDefinition();
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterGenericHandler(handlerType, typeof(Order)));

        var exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        exception.Problems.ShouldContain(p =>
            p.Code == "RF0004" && p.Subject == handlerType && p.Message.Contains("could not be loaded"));
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

        exception.Problems.ShouldContain(p =>
            p.Code == "RF0005" && p.Subject == typeof(AuditHandler<>) && p.Message.Contains("no closing types"));
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

        exception.Problems.ShouldContain(p =>
            p.Code == "RF0006" && p.Subject == typeof(List<>) && p.Message.Contains("is not a closed type"));
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

        exception.Problems.ShouldContain(p => p.Message.Contains("System.String"));
        exception.Problems.ShouldContain(p => p.Message.Contains("ConstrainedHandler") && p.Message.Contains(nameof(Plain)));
        exception.Problems.ShouldContain(p => p.Message.Contains(nameof(Lonely)));
    }

    [Fact]
    public void Given_Same_Invalid_Declaration_In_Two_Calls_When_Resolving_Dispatcher_Then_Validation_Reports_Problem_Once()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterGenericHandler(typeof(string), typeof(Order)));
        services.AddRequestFlow(o => o.RegisterGenericHandler(typeof(string), typeof(Order)));

        var exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        exception.Problems.Count(p => p.Message.Contains("System.String")).ShouldBe(1);
    }

    [Fact]
    public async Task Given_A_Generic_Stream_Handler_Closed_Over_A_Request_When_Enumerating_Then_It_Handles_It()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterGenericHandler(typeof(GenericStreamHandler<>), typeof(Counted));
            o.AllowUnhandledRequests();
        });
        IStreamDispatcher dispatcher = services.BuildServiceProvider().CreateScope().ServiceProvider
            .GetRequiredService<IStreamDispatcher>();

        List<int> items = await dispatcher.Stream(new Counted()).CollectAsync();

        items.ShouldBe([1]);
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

    // A definition that loaded while an interface it implements did not; asking for its interfaces
    // throws. The generic members forward explicitly so the shape checks before the interface read pass.
    private sealed class UnloadableInterfacesDefinition() : TypeDelegator(typeof(AuditHandler<>))
    {
        public override bool IsGenericTypeDefinition => true;

        public override Type[] GetGenericArguments()
            => typeof(AuditHandler<>).GetGenericArguments();

        public override Type[] GetInterfaces()
            => throw new TypeLoadException("Could not load type 'Contracts.IAudited'.");
    }

    #endregion
}
