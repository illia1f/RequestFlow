using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class ManualEventHandlerRegistrationTests
{
    [Fact]
    public async Task Given_A_Handler_Whose_Assembly_Is_Not_Scanned_When_Adding_It_Manually_Then_Publish_Reaches_It()
    {
        using ServiceProvider provider = Build(o => o.AddEventHandler<PortedHandler>());

        await provider.GetRequiredService<IEventPublisher>().PublishAsync(new PortedEvent());

        PortedHandler.Deliveries.ShouldBe(1);
    }

    [Fact]
    public async Task Given_A_Two_Contract_Handler_When_Adding_It_Manually_Then_Both_Events_Are_Delivered()
    {
        using ServiceProvider provider = Build(o => o.AddEventHandler<TwoPortHandler>());
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();

        await publisher.PublishAsync(new TwoPortAlphaEvent());
        await publisher.PublishAsync(new TwoPortBetaEvent());

        TwoPortHandler.AlphaDeliveries.ShouldBe(1);
        TwoPortHandler.BetaDeliveries.ShouldBe(1);
    }

    [Fact]
    public async Task Given_A_Closed_Generic_Handler_When_Adding_It_Manually_Then_Publish_Reaches_It()
    {
        using ServiceProvider provider = Build(o =>
            o.AddEventHandler<GenericPortHandler<PortedGenericEvent>>());

        await provider.GetRequiredService<IEventPublisher>().PublishAsync(new PortedGenericEvent());

        GenericPortHandler<PortedGenericEvent>.Deliveries.ShouldBe(1);
    }

    [Fact]
    public async Task Given_A_Scanned_Handler_When_Also_Added_Manually_Then_It_Is_Delivered_Once()
    {
        using ServiceProvider provider = Build(o => o
            .RegisterHandlersFromAssemblyContaining<ManualEventHandlerRegistrationTests>()
            .AddEventHandler<DedupHandler>());

        await provider.GetRequiredService<IEventPublisher>().PublishAsync(new DedupEvent());

        DedupHandler.Deliveries.ShouldBe(1);
    }

    [Fact]
    public void Given_A_Scanned_Handler_When_Also_Added_Manually_Then_One_Descriptor_Is_Added()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<ManualEventHandlerRegistrationTests>()
            .AddEventHandler<DedupHandler>());

        services.Count(descriptor => descriptor.ServiceType == typeof(DedupHandler)).ShouldBe(1);
    }

    [Fact]
    public void Given_The_Same_Manual_Registration_Repeated_When_Registering_Then_One_Subscription_Is_Kept()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .AddEventHandler<LifetimeProbeHandler>()
            .AddEventHandler<LifetimeProbeHandler>());
        services.AddRequestFlow(o => o.AddEventHandler<LifetimeProbeHandler>());

        services.Count(descriptor => descriptor.ServiceType == typeof(LifetimeProbeHandler))
            .ShouldBe(1);
        RequestFlowRegistry registry = RegistryOf(services);
        registry.EventHandlers.Count(handler => handler.HandlerType == typeof(LifetimeProbeHandler))
            .ShouldBe(1);
    }

    [Fact]
    public void Given_Two_Calls_Registering_One_Handler_Under_Different_Lifetimes_When_Registering_Then_The_First_Call_Wins()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o.AddEventHandler<LifetimeProbeHandler>());
        services.AddRequestFlow(o => o
            .WithScopedHandlers()
            .AddEventHandler<LifetimeProbeHandler>());

        ServiceDescriptor descriptor = services
            .Single(candidate => candidate.ServiceType == typeof(LifetimeProbeHandler));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);
    }

    [Fact]
    public void Given_Scoped_Handlers_Declared_After_The_Manual_Add_When_Registering_Then_The_Handler_Is_Scoped()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .AddEventHandler<OrderProbeHandler>()
            .WithScopedHandlers());

        ServiceDescriptor descriptor = services
            .Single(candidate => candidate.ServiceType == typeof(OrderProbeHandler));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Given_An_Interface_When_Adding_It_Manually_Then_Freeze_Reports_The_Interface_Problem()
    {
        using ServiceProvider provider = Build(o =>
            o.AddEventHandler<IEventHandler<PortedEvent>>());

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.ValidateRequestFlow());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(ProblemCodes.EventHandlerIsInterface);
        problem.Subject.ShouldBe(typeof(IEventHandler<PortedEvent>));
    }

    [Fact]
    public void Given_An_Abstract_Class_When_Adding_It_Manually_Then_Freeze_Reports_The_Abstract_Problem()
    {
        using ServiceProvider provider = Build(o => o.AddEventHandler<AbstractPortHandler>());

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.ValidateRequestFlow());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(ProblemCodes.EventHandlerAbstract);
        problem.Subject.ShouldBe(typeof(AbstractPortHandler));
    }

    [Fact]
    public void Given_A_Class_Without_The_Contract_When_Adding_It_Manually_Then_Freeze_Reports_The_Missing_Contract_Problem()
    {
        using ServiceProvider provider = Build(o => o.AddEventHandler<NotAHandler>());

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.ValidateRequestFlow());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(ProblemCodes.EventHandlerMissingContract);
        problem.Subject.ShouldBe(typeof(NotAHandler));
    }

    [Fact]
    public void Given_A_Scanned_Handler_When_Excluded_Then_No_Descriptor_Is_Added()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<ManualEventHandlerRegistrationTests>()
            .ExcludeEventHandler<DroppedHandler>());

        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(DroppedHandler));
    }

    [Fact]
    public async Task Given_A_Scanned_Handler_When_Excluded_Then_Publish_Skips_It()
    {
        using ServiceProvider provider = Build(o => o
            .RegisterHandlersFromAssemblyContaining<ManualEventHandlerRegistrationTests>()
            .ExcludeEventHandler<DroppedHandler>());

        await provider.GetRequiredService<IEventPublisher>().PublishAsync(new ExcludedEvent());

        KeptHandler.Deliveries.ShouldBe(1);
        DroppedHandler.Deliveries.ShouldBe(0);
    }

    [Fact]
    public void Given_An_Excluded_Handler_When_Freezing_Then_The_Model_Has_No_Subscription_For_It()
    {
        var rule = new CapturingRule();
        var services = new ServiceCollection();
        services.AddSingleton<IRequestFlowValidationRule>(rule);
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<ManualEventHandlerRegistrationTests>()
            .ExcludeEventHandler<DroppedHandler>());
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.ValidateRequestFlow();

        rule.Model!.EventSubscriptions
            .ShouldNotContain(subscription => subscription.HandlerType == typeof(DroppedHandler));
    }

    [Fact]
    public void Given_An_Exclusion_In_A_Later_Call_When_Registering_Then_The_Earlier_Scan_Keeps_The_Handler()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o =>
            o.RegisterHandlersFromAssemblyContaining<ManualEventHandlerRegistrationTests>());
        services.AddRequestFlow(o => o.ExcludeEventHandler<DroppedHandler>());

        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(DroppedHandler));
    }

    [Fact]
    public void Given_An_Exclusion_In_The_First_Scan_When_A_Later_Call_Names_The_Same_Assembly_Then_The_Handler_Stays_Excluded()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<ManualEventHandlerRegistrationTests>()
            .ExcludeEventHandler<DroppedHandler>());
        services.AddRequestFlow(o =>
            o.RegisterHandlersFromAssemblyContaining<ManualEventHandlerRegistrationTests>());

        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(DroppedHandler));
    }

    [Fact]
    public void Given_A_Dual_Role_Handler_When_Excluded_Then_The_Request_Contract_Stays()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<ManualEventHandlerRegistrationTests>()
            .ExcludeEventHandler<DualPortHandler>());

        services.ShouldContain(descriptor =>
            descriptor.ServiceType == typeof(IRequestHandler<PortPing, string>));
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(DualPortHandler));
    }

    [Fact]
    public async Task Given_An_Excluded_Handler_When_Also_Added_Manually_Then_Publish_Reaches_It()
    {
        using ServiceProvider provider = Build(o => o
            .RegisterHandlersFromAssemblyContaining<ManualEventHandlerRegistrationTests>()
            .ExcludeEventHandler<ManualWinsHandler>()
            .AddEventHandler<ManualWinsHandler>());

        await provider.GetRequiredService<IEventPublisher>().PublishAsync(new ManualWinsEvent());

        ManualWinsHandler.Deliveries.ShouldBe(1);
    }

    #region Helpers

    private static ServiceProvider Build(Action<RequestFlowOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(configure);
        return services.BuildServiceProvider();
    }

    private static RequestFlowRegistry RegistryOf(ServiceCollection services)
        => services
            .Single(descriptor => descriptor.ImplementationInstance is RequestFlowRegistry)
            .ImplementationInstance
            .ShouldBeOfType<RequestFlowRegistry>();

    public sealed record PortedEvent : IEvent;

    public sealed class PortedHandler : IEventHandler<PortedEvent>
    {
        public static int Deliveries;

        public Task HandleAsync(PortedEvent @event, CancellationToken cancellationToken)
        {
            Deliveries++;
            return Task.CompletedTask;
        }
    }

    public sealed record TwoPortAlphaEvent : IEvent;

    public sealed record TwoPortBetaEvent : IEvent;

    public sealed class TwoPortHandler :
        IEventHandler<TwoPortAlphaEvent>,
        IEventHandler<TwoPortBetaEvent>
    {
        public static int AlphaDeliveries;
        public static int BetaDeliveries;

        public Task HandleAsync(TwoPortAlphaEvent @event, CancellationToken cancellationToken)
        {
            AlphaDeliveries++;
            return Task.CompletedTask;
        }

        public Task HandleAsync(TwoPortBetaEvent @event, CancellationToken cancellationToken)
        {
            BetaDeliveries++;
            return Task.CompletedTask;
        }
    }

    public sealed record PortedGenericEvent : IEvent;

    // Open generic, so the scan skips it; only a manual add can close it.
    public sealed class GenericPortHandler<TEvent> : IEventHandler<TEvent>
        where TEvent : IEvent
    {
        public static int Deliveries;

        public Task HandleAsync(TEvent @event, CancellationToken cancellationToken)
        {
            Deliveries++;
            return Task.CompletedTask;
        }
    }

    public sealed record DedupEvent : IEvent;

    public sealed class DedupHandler : IEventHandler<DedupEvent>
    {
        public static int Deliveries;

        public Task HandleAsync(DedupEvent @event, CancellationToken cancellationToken)
        {
            Deliveries++;
            return Task.CompletedTask;
        }
    }

    public sealed record LifetimeProbeEvent : IEvent;

    public sealed class LifetimeProbeHandler : IEventHandler<LifetimeProbeEvent>
    {
        public Task HandleAsync(LifetimeProbeEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed record OrderProbeEvent : IEvent;

    public sealed class OrderProbeHandler : IEventHandler<OrderProbeEvent>
    {
        public Task HandleAsync(OrderProbeEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public abstract class AbstractPortHandler : IEventHandler<PortedEvent>
    {
        public Task HandleAsync(PortedEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class NotAHandler
    { }

    public sealed record ExcludedEvent : IEvent;

    public sealed class KeptHandler : IEventHandler<ExcludedEvent>
    {
        public static int Deliveries;

        public Task HandleAsync(ExcludedEvent @event, CancellationToken cancellationToken)
        {
            Deliveries++;
            return Task.CompletedTask;
        }
    }

    public sealed class DroppedHandler : IEventHandler<ExcludedEvent>
    {
        public static int Deliveries;

        public Task HandleAsync(ExcludedEvent @event, CancellationToken cancellationToken)
        {
            Deliveries++;
            return Task.CompletedTask;
        }
    }

    public sealed record PortPing : IRequest<string>;

    public sealed record DualPortEvent : IEvent;

    public sealed class DualPortHandler : IRequestHandler<PortPing, string>, IEventHandler<DualPortEvent>
    {
        public Task<string> HandleAsync(PortPing request, CancellationToken cancellationToken)
            => Task.FromResult("pong");

        public Task HandleAsync(DualPortEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed record ManualWinsEvent : IEvent;

    public sealed class ManualWinsHandler : IEventHandler<ManualWinsEvent>
    {
        public static int Deliveries;

        public Task HandleAsync(ManualWinsEvent @event, CancellationToken cancellationToken)
        {
            Deliveries++;
            return Task.CompletedTask;
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
