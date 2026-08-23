using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Events;

public sealed class EventStrategyRuntimeTests
{
    [Fact]
    public async Task Given_A_Custom_Strategy_When_Publishing_Then_It_Receives_The_Frozen_Delivery()
    {
        var marker = new Marker();
        var strategy = new CapturingStrategy();
        using ServiceProvider provider = BuildProvider<StrategyEvent, CapturingStrategy>(
            services =>
            {
                services.AddSingleton(marker);
                services.AddSingleton(strategy);
            });
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();
        var @event = new StrategyEvent();

        await publisher.PublishAsync(@event);

        strategy.Event.ShouldBeSameAs(@event);
        strategy.EventType.ShouldBe(typeof(StrategyEvent));
        strategy.Marker.ShouldBeSameAs(marker);
        EventSubscription subscription = strategy.Subscriptions[0];
        subscription.HandlerType.ShouldBe(typeof(StrategyEventHandler));
        subscription.DeclaredEventType.ShouldBe(typeof(StrategyEvent));
        @event.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Given_A_Fail_Fast_Strategy_When_The_First_Handler_Fails_Then_Later_Handlers_Are_Skipped()
    {
        using ServiceProvider provider = BuildProvider<FailFastEvent, FailFastPublishStrategy>();
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();
        provider.GetRequiredService<EventMap>().TryGetPlanFor(
            typeof(FailFastEvent), out EventPlan? plan).ShouldBeTrue();
        var failure = new InvalidOperationException("failure");
        var @event = new FailFastEvent(failure);

        EventPublishException exception = await Should.ThrowAsync<EventPublishException>(
            () => publisher.PublishAsync(@event));

        @event.Calls.ShouldBe(["fail"]);
        exception.Failures.ShouldHaveSingleItem().Exception.ShouldBeSameAs(failure);
        exception.SkippedHandlerCount.ShouldBe(plan!.Handlers.Count - 1);
    }

    [Fact]
    public async Task Given_A_Fail_Fast_Strategy_When_The_First_Handler_Returns_A_Canceled_Task_Then_Later_Handlers_Are_Skipped()
    {
        using ServiceProvider provider =
            BuildProvider<FailFastCanceledEvent, FailFastPublishStrategy>();
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();
        provider.GetRequiredService<EventMap>().TryGetPlanFor(
            typeof(FailFastCanceledEvent), out EventPlan? plan).ShouldBeTrue();
        using var source = new CancellationTokenSource();
        source.Cancel();
        var @event = new FailFastCanceledEvent(source.Token);

        EventPublishException exception = await Should.ThrowAsync<EventPublishException>(
            () => publisher.PublishAsync(@event));

        @event.Calls.ShouldBe(["cancel"]);
        exception.Failures.ShouldHaveSingleItem().Exception
            .ShouldBeAssignableTo<OperationCanceledException>();
        exception.SkippedHandlerCount.ShouldBe(plan!.Handlers.Count - 1);
    }

    [Fact]
    public async Task Given_A_Strategy_That_Drops_A_Failure_When_Publishing_Then_The_Publish_Succeeds()
    {
        using ServiceProvider provider = BuildProvider<DroppedFailureEvent, DroppingStrategy>();
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();
        var @event = new DroppedFailureEvent();

        await publisher.PublishAsync(@event);

        @event.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Given_A_Strategy_Returning_A_Null_Task_When_Publishing_Then_Throws_The_Typed_Exception()
    {
        using ServiceProvider provider = BuildProvider<NullStrategyEvent, NullTaskStrategy>();
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();

        EventStrategyNullTaskException exception =
            await Should.ThrowAsync<EventStrategyNullTaskException>(
                () => publisher.PublishAsync(new NullStrategyEvent()));

        exception.StrategyType.ShouldBe(typeof(NullTaskStrategy));
    }

    [Fact]
    public async Task Given_A_Strategy_Constructor_That_Throws_When_Publishing_Then_The_Failure_Is_Inside_The_Task()
    {
        using ServiceProvider provider = BuildProvider<ThrowingStrategyEvent, ThrowingStrategy>();
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();

        Task publish = publisher.PublishAsync(new ThrowingStrategyEvent());

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => publish);
        exception.Message.ShouldBe(ThrowingStrategy.Message);
    }

    [Fact]
    public async Task Given_A_Strategy_Omits_The_Entry_Token_When_Publishing_Then_The_Handler_Receives_The_Publishing_Token()
    {
        using ServiceProvider provider = BuildProvider<TokenEvent, OmittedTokenStrategy>();
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();
        var @event = new TokenEvent();
        using var source = new CancellationTokenSource();

        await publisher.PublishAsync(@event, source.Token);

        @event.ObservedToken.ShouldBe(source.Token);
    }

    [Fact]
    public async Task Given_A_Custom_Strategy_And_A_Known_Empty_Plan_When_Publishing_Then_The_Strategy_Runs()
    {
        var strategy = new EmptyPlanStrategy();
        var services = new ServiceCollection();
        services.AddSingleton(strategy);
        using ServiceProvider provider = services.BuildServiceProvider();
        var registry = new RequestFlowRegistry();
        registry.Add([], [], [typeof(EmptyPlanEvent)], []);
        registry.AllowUnhandledEvents();
        registry.AddEventStrategyDeclarations([
            new EventStrategyDeclaration(
                typeof(EmptyPlanEvent),
                typeof(EmptyPlanStrategy),
                ServiceLifetime.Singleton),
        ]);
        FrozenPlans plans = registry.Freeze(provider);
        var publisher = new EventPublisher(plans.Events, provider);

        await publisher.PublishAsync(new EmptyPlanEvent());

        strategy.DeliveryCount.ShouldBe(0);
    }

    [Fact]
    public async Task Given_A_Strategy_Abandons_A_Started_Entry_When_The_Handler_Fails_Then_The_Entry_Task_Returns_The_Failure()
    {
        var strategy = new AbandoningStrategy();
        using ServiceProvider provider = BuildProvider<AbandonedEntryEvent, AbandoningStrategy>(
            services => services.AddSingleton(strategy));
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();
        var failure = new InvalidOperationException("late failure");
        var @event = new AbandonedEntryEvent();

        Task publish = publisher.PublishAsync(@event);

        publish.Status.ShouldBe(TaskStatus.RanToCompletion);
        strategy.EntryTask.ShouldNotBeNull().IsCompleted.ShouldBeFalse();
        @event.Gate.SetException(failure);
        EventHandlerFailure? entryFailure = await strategy.EntryTask;
        entryFailure.ShouldNotBeNull().Exception.ShouldBeSameAs(failure);
        strategy.EntryTask.IsFaulted.ShouldBeFalse();
    }

    [Fact]
    public async Task Given_A_Strategy_Uses_RunAsync_When_The_First_Handler_Fails_Then_The_Raw_Failure_Propagates_And_Later_Handlers_Are_Skipped()
    {
        using ServiceProvider provider = BuildProvider<RawRethrowEvent, RunAsyncStrategy>();
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();
        var failure = new InvalidOperationException("raw failure");
        var @event = new RawRethrowEvent(failure);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => publisher.PublishAsync(@event));

        exception.ShouldBeSameAs(failure);
        @event.Calls.ShouldBe(["fail"]);
    }

    #region Helpers

    private static ServiceProvider BuildProvider<TEvent, TStrategy>(
        Action<IServiceCollection>? configure = null)
        where TEvent : IEvent
        where TStrategy : class, IEventPublishStrategy
    {
        var services = new ServiceCollection();
        configure?.Invoke(services);
        services.AddRequestFlow(options => options
            .RegisterHandlersFromAssemblyContaining<EventStrategyRuntimeTests>()
            .PublishEventsWith<TEvent, TStrategy>());
        return services.BuildServiceProvider();
    }

    public sealed class Marker;

    public sealed record StrategyEvent : IEvent
    {
        public int Calls { get; set; }
    }

    public sealed class StrategyEventHandler : IEventHandler<StrategyEvent>
    {
        public Task HandleAsync(StrategyEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls++;
            return Task.CompletedTask;
        }
    }

    public sealed class CapturingStrategy : IEventPublishStrategy
    {
        public IEvent? Event { get; private set; }

        public Type? EventType { get; private set; }

        public Marker? Marker { get; private set; }

        public List<EventSubscription> Subscriptions { get; } = [];

        public async Task PublishAsync(
            EventDelivery delivery,
            CancellationToken cancellationToken)
        {
            Event = delivery.Event;
            EventType = delivery.EventType;
            Marker = delivery.Services.GetRequiredService<Marker>();
            for (int i = 0; i < delivery.Count; i++)
            {
                Subscriptions.Add(delivery[i]);
                await delivery.RunAsync(i, cancellationToken);
            }
        }
    }

    public sealed record FailFastEvent(Exception Failure) : IEvent
    {
        public List<string> Calls { get; } = [];
    }

    public sealed class AlphaFailFastHandler : IEventHandler<FailFastEvent>
    {
        public Task HandleAsync(FailFastEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("fail");
            return Task.FromException(@event.Failure);
        }
    }

    public sealed class ZuluFailFastHandler : IEventHandler<FailFastEvent>
    {
        public Task HandleAsync(FailFastEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("after");
            return Task.CompletedTask;
        }
    }

    public sealed record FailFastCanceledEvent(CancellationToken HandlerToken) : IEvent
    {
        public List<string> Calls { get; } = [];
    }

    public sealed class AlphaFailFastCanceledHandler : IEventHandler<FailFastCanceledEvent>
    {
        public Task HandleAsync(FailFastCanceledEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("cancel");
            return Task.FromCanceled(@event.HandlerToken);
        }
    }

    public sealed class ZuluFailFastCanceledHandler : IEventHandler<FailFastCanceledEvent>
    {
        public Task HandleAsync(FailFastCanceledEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("after");
            return Task.CompletedTask;
        }
    }

    public sealed record DroppedFailureEvent : IEvent
    {
        public int Calls { get; set; }
    }

    public sealed class DroppedFailureHandler : IEventHandler<DroppedFailureEvent>
    {
        public Task HandleAsync(
            DroppedFailureEvent @event,
            CancellationToken cancellationToken)
        {
            @event.Calls++;
            return Task.FromException(new InvalidOperationException("dropped"));
        }
    }

    public sealed class DroppingStrategy : IEventPublishStrategy
    {
        public async Task PublishAsync(
            EventDelivery delivery,
            CancellationToken cancellationToken)
        {
            for (int i = 0; i < delivery.Count; i++)
                await delivery.StartAsync(i, cancellationToken);
        }
    }

    public sealed record NullStrategyEvent : IEvent;

    public sealed class NullStrategyEventHandler : IEventHandler<NullStrategyEvent>
    {
        public Task HandleAsync(
            NullStrategyEvent @event,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class NullTaskStrategy : IEventPublishStrategy
    {
        public Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken)
            => null!;
    }

    public sealed record ThrowingStrategyEvent : IEvent;

    public sealed class ThrowingStrategyEventHandler : IEventHandler<ThrowingStrategyEvent>
    {
        public Task HandleAsync(
            ThrowingStrategyEvent @event,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class ThrowingStrategy : IEventPublishStrategy
    {
        public const string Message = "strategy construction failed";

        public ThrowingStrategy()
            => throw new InvalidOperationException(Message);

        public Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed record TokenEvent : IEvent
    {
        public CancellationToken ObservedToken { get; set; }
    }

    public sealed class TokenEventHandler : IEventHandler<TokenEvent>
    {
        public Task HandleAsync(TokenEvent @event, CancellationToken cancellationToken)
        {
            @event.ObservedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    public sealed class OmittedTokenStrategy : IEventPublishStrategy
    {
        public async Task PublishAsync(
            EventDelivery delivery,
            CancellationToken cancellationToken)
            => await delivery.RunAsync(0);
    }

    public sealed record EmptyPlanEvent : IEvent;

    public sealed class EmptyPlanStrategy : IEventPublishStrategy
    {
        public int DeliveryCount { get; private set; } = -1;

        public Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken)
        {
            DeliveryCount = delivery.Count;
            return Task.CompletedTask;
        }
    }

    public sealed record AbandonedEntryEvent : IEvent
    {
        public TaskCompletionSource<object?> Gate { get; } = new();
    }

    public sealed class AbandonedEntryHandler : IEventHandler<AbandonedEntryEvent>
    {
        public Task HandleAsync(
            AbandonedEntryEvent @event,
            CancellationToken cancellationToken)
            => @event.Gate.Task;
    }

    public sealed class AbandoningStrategy : IEventPublishStrategy
    {
        public Task<EventHandlerFailure?>? EntryTask { get; private set; }

        public Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken)
        {
            EntryTask = delivery.StartAsync(0, cancellationToken);
            return Task.CompletedTask;
        }
    }

    public sealed record RawRethrowEvent(Exception Failure) : IEvent
    {
        public List<string> Calls { get; } = [];
    }

    public sealed class AlphaRawRethrowHandler : IEventHandler<RawRethrowEvent>
    {
        public Task HandleAsync(RawRethrowEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("fail");
            return Task.FromException(@event.Failure);
        }
    }

    public sealed class ZuluRawRethrowHandler : IEventHandler<RawRethrowEvent>
    {
        public Task HandleAsync(RawRethrowEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("after");
            return Task.CompletedTask;
        }
    }

    public sealed class RunAsyncStrategy : IEventPublishStrategy
    {
        public async Task PublishAsync(
            EventDelivery delivery,
            CancellationToken cancellationToken)
        {
            for (int i = 0; i < delivery.Count; i++)
                await delivery.RunAsync(i, cancellationToken);
        }
    }

    #endregion
}
