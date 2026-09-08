using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class EventRegistrationTests
{
    [Fact]
    public void Given_Two_Contract_Event_Handler_When_Registering_Then_Concrete_Handler_Is_Registered_Once()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o =>
            o.RegisterHandlersFromAssemblyContaining<EventRegistrationTests>());

        services.Count(descriptor => descriptor.ServiceType == typeof(TwoContractHandler))
            .ShouldBe(1);
    }

    [Fact]
    public void Given_Event_Handlers_When_Registering_Then_Event_Contracts_Are_Not_Service_Keys()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o =>
            o.RegisterHandlersFromAssemblyContaining<EventRegistrationTests>());

        services.ShouldNotContain(descriptor =>
            descriptor.ServiceType.IsGenericType
            && descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IEventHandler<>));
    }

    [Fact]
    public void Given_Scoped_Event_Handlers_When_Registering_Then_Concrete_Handler_Is_Scoped()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<EventRegistrationTests>()
            .WithScopedHandlers());

        ServiceDescriptor descriptor = services
            .Single(candidate => candidate.ServiceType == typeof(TwoContractHandler));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Given_Known_Event_Universe_When_Freezing_Then_Only_Concrete_Closed_Events_Have_Plans()
    {
        using ServiceProvider provider = BuildProvider();
        EventMap map = provider.GetRequiredService<EventMap>();

        map.TryGetPlanFor(typeof(ConcreteEvent), out _).ShouldBeTrue();
        map.TryGetPlanFor(typeof(GenericEvent<int>), out _).ShouldBeTrue();
        map.TryGetPlanFor(typeof(AbstractEvent), out _).ShouldBeFalse();
        map.TryGetPlanFor(typeof(IMarkerEvent), out _).ShouldBeFalse();
        map.TryGetPlanFor(typeof(GenericEvent<>), out _).ShouldBeFalse();
    }

    [Fact]
    public void Given_Dispatch_And_Event_Maps_When_Resolving_Both_Then_Registry_Freezes_Once()
    {
        var rule = new CountingRule();
        var services = new ServiceCollection();
        services.AddSingleton<IRequestFlowValidationRule>(rule);
        services.AddRequestFlow(o =>
            o.RegisterHandlersFromAssemblyContaining<EventRegistrationTests>());
        using ServiceProvider provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<DispatchMap>();
        _ = provider.GetRequiredService<EventMap>();

        rule.Calls.ShouldBe(1);
    }

    [Fact]
    public void Given_One_Freeze_When_Building_Validation_And_Event_Plans_Then_Handler_Metadata_Is_Shared()
    {
        var rule = new CapturingRule();
        var services = new ServiceCollection();
        services.AddSingleton<IRequestFlowValidationRule>(rule);
        services.AddRequestFlow(o =>
            o.RegisterHandlersFromAssemblyContaining<EventRegistrationTests>());
        using ServiceProvider provider = services.BuildServiceProvider();

        EventMap map = provider.GetRequiredService<EventMap>();
        map.TryGetPlanFor(typeof(ConcreteEvent), out EventPlan? plan).ShouldBeTrue();

        EventHandlerModel captured = rule.Model!.Events
            .Single(@event => @event.EventType == typeof(ConcreteEvent))
            .Handlers
            .Single(handler => handler.HandlerType == typeof(TwoContractHandler));
        plan!.Handlers.Single(handler => handler.HandlerType == typeof(TwoContractHandler))
            .ShouldBeSameAs(captured);
    }

    [Fact]
    public void Given_Event_Options_In_Later_Call_When_Registering_Then_Options_Are_Sticky()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
            o.RegisterHandlersFromAssemblyContaining<EventRegistrationTests>());
        services.AddRequestFlow(o => o
            .PublishEventsInParallel()
            .AllowUnhandledEvents()
            .DisallowUnusedEventHandlers());

        RequestFlowRegistry registry = services
            .Single(descriptor => descriptor.ImplementationInstance is RequestFlowRegistry)
            .ImplementationInstance
            .ShouldBeOfType<RequestFlowRegistry>();

        registry.EventStrategyDeclarations.ShouldHaveSingleItem().StrategyType
            .ShouldBe(typeof(ParallelPublishStrategy));
        registry.AllUnhandledEventsAllowed.ShouldBeTrue();
        registry.UnusedEventHandlersDisallowed.ShouldBeTrue();
    }

    [Fact]
    public void Given_A_Custom_Strategy_When_Registering_Then_It_Is_Registered_As_A_Singleton()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(options =>
            options.PublishAllEventsWith<CustomStrategy>());

        ServiceDescriptor descriptor = services.Single(candidate =>
            candidate.ServiceType == typeof(CustomStrategy));
        descriptor.ImplementationType.ShouldBe(typeof(CustomStrategy));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void Given_A_User_Registered_Strategy_Instance_When_Registering_Then_The_Existing_Descriptor_Is_Kept()
    {
        var services = new ServiceCollection();
        var instance = new CustomStrategy();
        services.AddSingleton(instance);

        services.AddRequestFlow(options =>
            options.PublishAllEventsWith<CustomStrategy>());

        ServiceDescriptor descriptor = services.Single(candidate =>
            candidate.ServiceType == typeof(CustomStrategy));
        descriptor.ImplementationInstance.ShouldBeSameAs(instance);
    }

    [Fact]
    public void Given_Built_In_Strategies_When_Registering_Then_No_Strategy_Descriptors_Are_Added()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(options =>
        {
            options.PublishAllEventsWith<SequentialPublishStrategy>();
            options.PublishEventsWith<ConcreteEvent, ParallelPublishStrategy>();
            options.PublishEventsWith<SecondEvent, FailFastPublishStrategy>();
        });

        services.ShouldNotContain(descriptor =>
            descriptor.ServiceType == typeof(SequentialPublishStrategy)
            || descriptor.ServiceType == typeof(ParallelPublishStrategy)
            || descriptor.ServiceType == typeof(FailFastPublishStrategy));
    }

    [Fact]
    public void Given_A_Custom_Strategy_Declared_Twice_When_Registering_Then_One_Descriptor_Is_Added()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(options =>
            options.PublishAllEventsWith<CustomStrategy>());
        services.AddRequestFlow(options =>
            options.PublishAllEventsWith<CustomStrategy>());

        services.Count(descriptor => descriptor.ServiceType == typeof(CustomStrategy))
            .ShouldBe(1);
        RequestFlowRegistry registry = services
            .Single(descriptor => descriptor.ImplementationInstance is RequestFlowRegistry)
            .ImplementationInstance
            .ShouldBeOfType<RequestFlowRegistry>();
        registry.EventStrategyDeclarations.Count.ShouldBe(1);
    }

    [Fact]
    public void Given_A_Stage_And_A_Strategy_When_Registering_Then_The_Strategy_Descriptor_Comes_After_The_Stage()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(options => options
            .RegisterHandlersFromAssemblyContaining<EventRegistrationTests>()
            .AddStage<PlainOrderingStage>()
            .PublishAllEventsWith<CustomStrategy>());

        int stageIndex = services.IndexOf(services.Single(descriptor =>
            descriptor.ServiceType == typeof(PlainOrderingStage)));
        int strategyIndex = services.IndexOf(services.Single(descriptor =>
            descriptor.ServiceType == typeof(CustomStrategy)));
        strategyIndex.ShouldBeGreaterThan(stageIndex);
    }

    [Fact]
    public void Given_A_Singleton_Stage_That_Also_Handles_Events_When_Freezing_Then_Reports_The_Lifetime_Collision()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<EventRegistrationTests>()
            .AddStage<DualRoleStage>(stage => stage.AsSingleton()));
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.ValidateRequestFlow());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(ProblemCodes.StageEventHandlerLifetime);
        problem.Subject.ShouldBe(typeof(DualRoleStage));
    }

    [Fact]
    public void Given_A_Transient_Stage_That_Also_Handles_Events_When_Freezing_Then_Both_Roles_Resolve_The_Same_Way()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<EventRegistrationTests>()
            .AddStage<DualRoleStage>());
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.ValidateRequestFlow();

        services.Where(descriptor => descriptor.ServiceType == typeof(DualRoleStage))
            .ShouldAllBe(descriptor => descriptor.Lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public void Given_A_Stage_Declared_Before_Its_Own_Assembly_Is_Scanned_When_Registering_Then_The_Event_Handler_Descriptor_Comes_Last()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssembly(typeof(RequestFlow.Tests.ValidationFixtures.Rooted).Assembly)
            .AddStage<LateScannedDualStage>());
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<EventRegistrationTests>()
            .WithScopedHandlers());

        services.Where(descriptor => descriptor.ServiceType == typeof(LateScannedDualStage))
            .Select(descriptor => descriptor.Lifetime)
            .ShouldBe([ServiceLifetime.Transient, ServiceLifetime.Scoped]);
    }

    [Fact]
    public void Given_A_Scoped_Class_In_Both_Handler_Roles_When_Resolving_In_One_Scope_Then_Each_Role_Gets_Its_Own_Instance()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<EventRegistrationTests>()
            .WithScopedHandlers());
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        object requestRole = scope.ServiceProvider.GetRequiredService<IRequestHandler<DualDuty, string>>();

        requestRole.ShouldNotBeSameAs(scope.ServiceProvider.GetRequiredService(typeof(DualDutyHandler)));
    }

    #region Helpers

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
            o.RegisterHandlersFromAssemblyContaining<EventRegistrationTests>());
        return services.BuildServiceProvider();
    }

    public sealed record ConcreteEvent : IMarkerEvent;

    public abstract record AbstractEvent : IMarkerEvent;

    public interface IMarkerEvent : IEvent;

    public sealed record SecondEvent : IEvent;

    public sealed record GenericEvent<T> : IEvent;

    public sealed record DualRoleEvent : IEvent;

    public sealed record DualPing : IRequest<string>;

    public sealed class DualPingHandler : IRequestHandler<DualPing, string>
    {
        public Task<string> HandleAsync(DualPing request, CancellationToken cancellationToken)
            => Task.FromResult("pong");
    }

    // One class in both roles: the stage descriptor and the event handler descriptor share the
    // concrete service key.
    public sealed class DualRoleStage : IRequestStage<DualPing, string>, IEventHandler<DualRoleEvent>
    {
        public Task<string> HandleAsync(
            DualPing request, Continuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);

        public Task HandleAsync(DualRoleEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class PlainOrderingStage : IRequestStage<DualPing, string>
    {
        public Task<string> HandleAsync(
            DualPing request,
            Continuation<string> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    public sealed record LateScannedEvent : IEvent;

    // Declared as a stage by a call that does not scan this assembly, so the event handler
    // descriptor for the same class lands after the stage descriptor.
    public sealed class LateScannedDualStage :
        IRequestStage<RequestFlow.Tests.ValidationFixtures.Rooted, int>,
        IEventHandler<LateScannedEvent>
    {
        public Task<int> HandleAsync(
            RequestFlow.Tests.ValidationFixtures.Rooted request,
            Continuation<int> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);

        public Task HandleAsync(LateScannedEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed record DualDuty : IRequest<string>;

    public sealed record DualDutyEvent : IEvent;

    // One class under two service keys: the request contract and its own concrete type.
    public sealed class DualDutyHandler : IRequestHandler<DualDuty, string>, IEventHandler<DualDutyEvent>
    {
        public Task<string> HandleAsync(DualDuty request, CancellationToken cancellationToken)
            => Task.FromResult("done");

        public Task HandleAsync(DualDutyEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class TwoContractHandler :
        IEventHandler<ConcreteEvent>,
        IEventHandler<SecondEvent>
    {
        public Task HandleAsync(ConcreteEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task HandleAsync(SecondEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class ClosedGenericHandler : IEventHandler<GenericEvent<int>>
    {
        public Task HandleAsync(GenericEvent<int> @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class CustomStrategy : IEventPublishStrategy
    {
        public Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    // Scanned, not referenced by name: its IEvent contract keeps every event in this assembly
    // handled, so removing it turns every freeze here into an RF0114 failure.
    public sealed class UniversalHandler : IEventHandler<IEvent>
    {
        public Task HandleAsync(IEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class OpenGenericHandler<T> : IEventHandler<GenericEvent<T>>
    {
        public Task HandleAsync(GenericEvent<T> @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class CountingRule : IRequestFlowValidationRule
    {
        public int Calls { get; private set; }

        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
        {
            Calls++;
            return [];
        }
    }

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
