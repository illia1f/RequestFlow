using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow.Tests.ValidationFixtures;

namespace RequestFlow.Tests.Unit;

public sealed class ModuleOptionsTests
{
    [Fact]
    public void Given_A_Later_Dispatcher_Lifetime_When_Validating_Then_The_First_Registration_Is_Kept()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(_ => { });
        services.AddRequestFlow(o => o.WithTransientDispatcher());
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.ValidateRequestFlow();

        services.Single(d => d.ServiceType == typeof(IRequestDispatcher)).Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_One_Exempt_Request_When_Validating_Modules_Then_Only_The_Other_Request_Is_Reported(bool exemptionFirst)
    {
        var services = new ServiceCollection();
        Action<RequestFlowOptions> exemption = o => o.AllowUnhandledRequest<ModuleRequest>();
        Action<RequestFlowOptions> registration = o => o.RegisterHandlersFromAssembly(
            new SelectedTypesAssembly(typeof(ModuleRequest), typeof(OtherRequest)));
        services.AddRequestFlow(exemptionFirst ? exemption : registration);
        services.AddRequestFlow(exemptionFirst ? registration : exemption);
        using ServiceProvider provider = services.BuildServiceProvider();

        var exception = Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(ProblemCodes.UnhandledRequest);
        problem.Subject.ShouldBe(typeof(OtherRequest));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_An_Exempt_Contracts_Assembly_When_Validating_Then_Another_Assembly_Remains_Checked(bool exemptionFirst)
    {
        var services = new ServiceCollection();
        Action<RequestFlowOptions> exemption = o => o.AllowUnhandledRequestsFromAssembly(typeof(Lonely).Assembly);
        Action<RequestFlowOptions> registration = o => o.RegisterHandlersFromAssembly(
            new SelectedTypesAssembly(typeof(Lonely), typeof(ModuleRequest)));
        services.AddRequestFlow(exemptionFirst ? exemption : registration);
        services.AddRequestFlow(exemptionFirst ? registration : exemption);
        using ServiceProvider provider = services.BuildServiceProvider();

        var exception = Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        exception.Problems.ShouldHaveSingleItem().Subject.ShouldBe(typeof(ModuleRequest));
    }

    [Fact]
    public void Given_An_Exempt_Event_When_Validating_Then_Another_Event_Remains_Checked()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.AddEvent<ModuleEvent>().AllowUnhandledEvent<ModuleEvent>());
        services.AddRequestFlow(o => o.AddEvent<OtherEvent>());
        using ServiceProvider provider = services.BuildServiceProvider();

        var exception = Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(ProblemCodes.UnhandledEvent);
        problem.Subject.ShouldBe(typeof(OtherEvent));
    }

    [Fact]
    public async Task Given_An_Exempt_Event_When_Publishing_Then_The_Known_Event_Has_An_Empty_Plan()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.AddEvent<ModuleEvent>().AllowUnhandledEvent<ModuleEvent>());
        using ServiceProvider provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IEventPublisher>().PublishAsync(new ModuleEvent());
    }

    [Fact]
    public void Given_An_Exempt_Request_When_Inspecting_Then_No_Handler_Plan_Is_Invented()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssembly(new SelectedTypesAssembly(typeof(ModuleRequest)))
            .AllowUnhandledRequest(typeof(ModuleRequest)));
        using ServiceProvider provider = services.BuildServiceProvider();

        Should.Throw<HandlerNotFoundException>(() => provider.InspectRequestFlow<ModuleRequest>());
    }

    [Fact]
    public void Given_A_Transient_Dispatcher_And_Later_Default_Options_When_Validating_Then_The_First_Lifetime_Is_Kept()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.WithTransientDispatcher());
        services.AddRequestFlow(_ => { });
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.ValidateRequestFlow();

        services.Single(d => d.ServiceType == typeof(IRequestDispatcher)).Lifetime.ShouldBe(ServiceLifetime.Transient);
    }

    [Fact]
    public void Given_Exemptions_When_Building_Another_Context_Then_The_First_Snapshot_Does_Not_Change()
    {
        var builder = new RequestFlowModelBuilder().AllowUnhandledRequest(typeof(ModuleRequest));
        RequestFlowValidationContext first = builder.BuildContext();

        RequestFlowValidationContext second = builder.AllowUnhandledRequest(typeof(OtherRequest)).BuildContext();

        first.AllowsUnhandledRequest(typeof(ModuleRequest)).ShouldBeTrue();
        first.AllowsUnhandledRequest(typeof(OtherRequest)).ShouldBeFalse();
        first.AllUnhandledRequestsAllowed.ShouldBeFalse();
        second.AllowsUnhandledRequest(typeof(OtherRequest)).ShouldBeTrue();
        Should.Throw<NotSupportedException>(() => ((IList<Type>)first.UnhandledRequestTypes).Clear());
    }

    [Theory]
    [InlineData(typeof(ModuleRequest))]
    [InlineData(typeof(OtherRequest))]
    [InlineData(typeof(StreamRequest))]
    [InlineData(typeof(VoidRequest))]
    [InlineData(typeof(ValueVoidRequest))]
    public void Given_An_Exempt_Request_Family_When_Validating_Then_Missing_Handler_Is_Permitted(Type requestType)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssembly(new SelectedTypesAssembly(requestType))
            .AllowUnhandledRequest(requestType));
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.ValidateRequestFlow();
    }

    [Fact]
    public void Given_An_Exempt_Base_Request_When_Validating_Then_A_Derived_Request_Is_Not_Exempt()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssembly(new SelectedTypesAssembly(typeof(Rooted), typeof(Orphaned)))
            .AllowUnhandledRequest<Rooted>());
        using ServiceProvider provider = services.BuildServiceProvider();

        var exception = Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        exception.Problems.ShouldHaveSingleItem().Subject.ShouldBe(typeof(Orphaned));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_An_Exempt_Event_Assembly_When_Validating_Then_Another_Assembly_Remains_Checked(bool exemptionFirst)
    {
        var services = new ServiceCollection();
        Action<RequestFlowOptions> exemption = o => o.AllowUnhandledEventsFromAssembly(typeof(UnhandledValidationEvent).Assembly);
        Action<RequestFlowOptions> registration = o => o.AddEvent<UnhandledValidationEvent>().AddEvent<ModuleEvent>();
        services.AddRequestFlow(exemptionFirst ? exemption : registration);
        services.AddRequestFlow(exemptionFirst ? registration : exemption);
        using ServiceProvider provider = services.BuildServiceProvider();

        var exception = Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        exception.Problems.ShouldHaveSingleItem().Subject.ShouldBe(typeof(ModuleEvent));
    }

    [Fact]
    public void Given_Only_An_Event_Exemption_When_Publishing_Then_The_Event_Is_Not_Registered()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.AllowUnhandledEvent(typeof(ModuleEvent)));
        using ServiceProvider provider = services.BuildServiceProvider();
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();

        Should.Throw<EventNotRegisteredException>(() => publisher.PublishAsync(new ModuleEvent()));
    }

    [Fact]
    public void Given_A_Request_Exemption_With_Duplicate_Handlers_When_Validating_Then_Duplicates_Are_Still_Reported()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o
            .AddHandler<FirstDuplicatedHandler>()
            .AddHandler<SecondDuplicatedHandler>()
            .AllowUnhandledRequest<Duplicated>());
        using ServiceProvider provider = services.BuildServiceProvider();

        var exception = Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        exception.Problems.ShouldHaveSingleItem().Code.ShouldBe(ProblemCodes.DuplicateHandler);
    }

    [Fact]
    public void Given_An_Exemption_When_A_Custom_Rule_Runs_Then_It_Sees_The_Effective_Policy()
    {
        var rule = new ContextRule();
        var services = new ServiceCollection();
        services.AddSingleton<IRequestFlowValidationRule>(rule);
        services.AddRequestFlow(o => o.AllowUnhandledRequest<ModuleRequest>()
            .AllowUnhandledEvent<ModuleEvent>());
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.ValidateRequestFlow();

        rule.Context.ShouldNotBeNull();
        rule.Context.AllowsUnhandledRequest(typeof(ModuleRequest)).ShouldBeTrue();
        rule.Context.AllowsUnhandledRequest(typeof(OtherRequest)).ShouldBeFalse();
        rule.Context.AllowsUnhandledEvent(typeof(ModuleEvent)).ShouldBeTrue();
        rule.Context.AllowsUnhandledEvent(typeof(OtherEvent)).ShouldBeFalse();
        rule.Context.AllUnhandledRequestsAllowed.ShouldBeFalse();
        rule.Context.AllUnhandledEventsAllowed.ShouldBeFalse();
    }

    [Fact]
    public void Given_Exemptions_In_One_Collection_When_Validating_Another_Then_They_Do_Not_Leak()
    {
        var first = new ServiceCollection();
        first.AddRequestFlow(o => o.AllowUnhandledRequest<ModuleRequest>());
        var second = new ServiceCollection();
        second.AddRequestFlow(o => o.RegisterHandlersFromAssembly(new SelectedTypesAssembly(typeof(ModuleRequest))));
        using ServiceProvider provider = second.BuildServiceProvider();

        var exception = Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        exception.Problems.ShouldHaveSingleItem().Code.ShouldBe(ProblemCodes.UnhandledRequest);
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(IRequest<int>))]
    [InlineData(typeof(OpenRequest<>))]
    public void Given_An_Invalid_Request_Exemption_When_Configuring_Then_Throws_Argument_Exception(Type requestType)
    {
        Should.Throw<ArgumentException>(() => new RequestFlowOptions().AllowUnhandledRequest(requestType));
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(IEvent))]
    [InlineData(typeof(DeadValidationEventBase))]
    public void Given_An_Invalid_Event_Exemption_When_Configuring_Then_Throws_Argument_Exception(Type eventType)
    {
        Should.Throw<ArgumentException>(() => new RequestFlowOptions().AllowUnhandledEvent(eventType));
    }

    [Fact]
    public void Given_Null_Exemption_Inputs_When_Configuring_Then_Throws_Argument_Null_Exception()
    {
        var options = new RequestFlowOptions();

        Should.Throw<ArgumentNullException>(() => options.AllowUnhandledRequest(null!));
        Should.Throw<ArgumentNullException>(() => options.AllowUnhandledEvent(null!));
        Should.Throw<ArgumentNullException>(() => options.AllowUnhandledRequestsFromAssembly(null!));
        Should.Throw<ArgumentNullException>(() => options.AllowUnhandledEventsFromAssembly(null!));
    }

    [Fact]
    public void Given_Builder_Assembly_Exemptions_When_Building_A_Context_Then_They_Apply_To_Their_Message_Families()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AllowUnhandledRequestsFromAssembly(typeof(Lonely).Assembly)
            .AllowUnhandledEventsFromAssembly(typeof(ModuleEvent).Assembly)
            .AllowUnhandledEvent(typeof(UnhandledValidationEvent))
            .BuildContext();

        context.AllowsUnhandledRequest(typeof(Lonely)).ShouldBeTrue();
        context.AllowsUnhandledRequest(typeof(ModuleRequest)).ShouldBeFalse();
        context.AllowsUnhandledEvent(typeof(ModuleEvent)).ShouldBeTrue();
        context.AllowsUnhandledEvent(typeof(UnhandledValidationEvent)).ShouldBeTrue();
        Should.Throw<NotSupportedException>(() => ((IList<Assembly>)context.UnhandledRequestAssemblies).Clear());
        Should.Throw<NotSupportedException>(() => ((IList<Assembly>)context.UnhandledEventAssemblies).Clear());
        Should.Throw<NotSupportedException>(() => ((IList<Type>)context.UnhandledEventTypes).Clear());
    }

    #region Helpers

    public sealed record ModuleRequest : IRequest<int>;
    public sealed record OtherRequest : IValueRequest<int>;
    public sealed record ModuleEvent : IEvent;
    public sealed record OtherEvent : IEvent;
    public sealed record StreamRequest : IStreamRequest<int>;
    public sealed record VoidRequest : IRequest;
    public sealed record ValueVoidRequest : IValueRequest;
    public sealed record OpenRequest<T> : IRequest<T>;

    public sealed class ModuleRequestHandler : IRequestHandler<ModuleRequest, int>
    {
        public Task<int> HandleAsync(ModuleRequest request, CancellationToken cancellationToken) => Task.FromResult(1);
    }

    public sealed class OtherRequestHandler : IValueRequestHandler<OtherRequest, int>
    {
        public ValueTask<int> HandleAsync(OtherRequest request, CancellationToken cancellationToken) => new(1);
    }

    public sealed class StreamRequestHandler : IStreamRequestHandler<StreamRequest, int>
    {
        public async IAsyncEnumerable<int> Handle(StreamRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield return 1;
        }
    }

    public sealed class VoidRequestHandler : IRequestHandler<VoidRequest>
    {
        public Task HandleAsync(VoidRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public sealed class ValueVoidRequestHandler : IValueRequestHandler<ValueVoidRequest>
    {
        public ValueTask HandleAsync(ValueVoidRequest request, CancellationToken cancellationToken) => default;
    }

    public sealed class ModuleEventHandler : IEventHandler<ModuleEvent>
    {
        public Task HandleAsync(ModuleEvent @event, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public sealed class OtherEventHandler : IEventHandler<OtherEvent>
    {
        public Task HandleAsync(OtherEvent @event, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ContextRule : IRequestFlowValidationRule
    {
        public RequestFlowValidationContext? Context { get; private set; }

        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
        {
            Context = context;
            return [];
        }
    }

    private sealed class SelectedTypesAssembly(params Type[] types) : Assembly
    {
        public override Type[] GetTypes() => types;
    }

    #endregion
}
