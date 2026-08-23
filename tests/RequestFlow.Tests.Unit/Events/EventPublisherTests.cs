using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Events;

public sealed class EventPublisherTests
{
    [Fact]
    public void Given_A_Null_Event_When_Publishing_Then_Throws_Argument_Null_Exception_Synchronously()
    {
        EventPublisher publisher = EmptyPublisher(typeof(KnownEvent));

        Action action = () => publisher.PublishAsync(null!);

        action.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("event");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_An_Unknown_Event_Type_When_Publishing_Then_Throws_Not_Registered_Exception_Synchronously(
        bool parallel)
    {
        EventPublisher publisher = EmptyPublisher(typeof(KnownEvent), parallel: parallel);

        Action action = () => publisher.PublishAsync(new UnknownEvent());

        EventNotRegisteredException exception = action.ShouldThrow<EventNotRegisteredException>();
        exception.EventType.ShouldBe(typeof(UnknownEvent));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_An_Unknown_Runtime_Subclass_When_Publishing_Then_Throws_Not_Registered_Exception_Synchronously(
        bool parallel)
    {
        EventPublisher publisher = EmptyPublisher(typeof(KnownBaseEvent), parallel: parallel);

        Action action = () => publisher.PublishAsync(new UnknownDerivedEvent());

        EventNotRegisteredException exception = action.ShouldThrow<EventNotRegisteredException>();
        exception.EventType.ShouldBe(typeof(UnknownDerivedEvent));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Known_Empty_Plan_When_Publishing_Then_Completes_Without_Resolving_A_Handler(
        bool parallel)
    {
        var services = Substitute.For<IServiceProvider>();
        EventPublisher publisher = EmptyPublisher(typeof(KnownEvent), services, parallel);

        await publisher.PublishAsync(new KnownEvent());

        services.DidNotReceive().GetService(Arg.Any<Type>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Pre_Canceled_Publishing_Token_When_Publishing_An_Empty_Plan_Then_Cancellation_Is_Returned_Asynchronously(
        bool parallel)
    {
        var services = Substitute.For<IServiceProvider>();
        EventPublisher publisher = EmptyPublisher(typeof(KnownEvent), services, parallel);
        using var source = new CancellationTokenSource();
        source.Cancel();

        Task publish = publisher.PublishAsync(new KnownEvent(), source.Token);
        EventPublishCanceledException exception = (await ExceptionFrom(publish))
            .ShouldBeOfType<EventPublishCanceledException>();

        exception.Failures.ShouldBeEmpty();
        exception.CancellationToken.ShouldBe(source.Token);
        exception.Message.ShouldContain("0 handlers were skipped");
        publish.IsCanceled.ShouldBeTrue();
        publish.Exception.ShouldBeNull();
        services.DidNotReceive().GetService(Arg.Any<Type>());
    }

    [Theory]
    [InlineData(false, ServiceLifetime.Scoped)]
    [InlineData(true, ServiceLifetime.Transient)]
    public void Given_A_Dispatcher_Lifetime_When_Registering_Then_Event_Publisher_Uses_That_Lifetime(
        bool transient, ServiceLifetime expected)
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(options =>
        {
            options.RegisterHandlersFromAssemblyContaining<EventPublisherTests>();
            if (transient)
                options.WithTransientDispatcher();
        });

        ServiceDescriptor descriptor = services.Single(candidate =>
            candidate.ServiceType == typeof(IEventPublisher));
        descriptor.ImplementationType.ShouldBe(typeof(EventPublisher));
        descriptor.Lifetime.ShouldBe(expected);
    }

    [Fact]
    public async Task Given_Sequential_Entries_When_Publishing_Then_Each_Handler_Is_Resolved_Immediately_Before_Invocation()
    {
        var timeline = new List<string>();
        using ServiceProvider provider = BuildProvider(services => services.AddSingleton(timeline));
        EventMap map = provider.GetRequiredService<EventMap>();
        var services = new TimelineProvider(
            provider,
            timeline,
            typeof(AlphaResolutionHandler),
            typeof(ZuluResolutionHandler));
        var publisher = new EventPublisher(map, services);

        await publisher.PublishAsync(new ResolutionEvent(timeline));

        timeline.ShouldBe([
            "resolve AlphaResolutionHandler",
            "handle AlphaResolutionHandler",
            "resolve ZuluResolutionHandler",
            "handle ZuluResolutionHandler",
        ]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Synchronously_Throwing_Handler_When_Publishing_Then_Later_Handlers_Run_And_The_Failure_Is_Returned(
        bool parallel)
    {
        using ServiceProvider provider = BuildProvider(parallel: parallel);
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();
        var failure = new InvalidOperationException("sync");
        var @event = new SynchronousThrowEvent(failure);

        Task publish = publisher.PublishAsync(@event);
        EventPublishException exception = await Should.ThrowAsync<EventPublishException>(() => publish);

        @event.Calls.ShouldBe(["throw", "after"]);
        EventHandlerFailure handlerFailure = exception.Failures.ShouldHaveSingleItem();
        handlerFailure.HandlerType.ShouldBe(typeof(AlphaSynchronousThrowHandler));
        handlerFailure.DeclaredEventType.ShouldBe(typeof(SynchronousThrowEvent));
        handlerFailure.Exception.ShouldBeSameAs(failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Handler_Returning_A_Null_Task_When_Publishing_Then_Later_Handlers_Run_And_The_Null_Task_Is_A_Failure(
        bool parallel)
    {
        using ServiceProvider provider = BuildProvider(parallel: parallel);
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();
        var @event = new NullTaskEvent();

        Task publish = publisher.PublishAsync(@event);
        EventPublishException exception = await Should.ThrowAsync<EventPublishException>(() => publish);

        @event.Calls.ShouldBe(["null", "after"]);
        EventHandlerFailure failure = exception.Failures.ShouldHaveSingleItem();
        failure.HandlerType.ShouldBe(typeof(AlphaNullTaskHandler));
        failure.DeclaredEventType.ShouldBe(typeof(NullTaskEvent));
        EventHandlerNullTaskException cause = failure.Exception
            .ShouldBeOfType<EventHandlerNullTaskException>();
        cause.EventType.ShouldBe(typeof(NullTaskEvent));
        cause.HandlerType.ShouldBe(typeof(AlphaNullTaskHandler));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_An_Unresolvable_Handler_When_Publishing_Then_Later_Handlers_Run_And_Resolution_Is_A_Failure(
        bool parallel)
    {
        using ServiceProvider provider = BuildProvider(parallel: parallel);
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();
        var @event = new UnresolvableEvent();

        Task publish = publisher.PublishAsync(@event);
        EventPublishException exception = await Should.ThrowAsync<EventPublishException>(() => publish);

        @event.Calls.ShouldBe(["after"]);
        EventHandlerFailure failure = exception.Failures.ShouldHaveSingleItem();
        failure.HandlerType.ShouldBe(typeof(AlphaUnresolvableHandler));
        failure.DeclaredEventType.ShouldBe(typeof(UnresolvableEvent));
        failure.Exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Handler_Task_With_Several_Exceptions_When_Publishing_Then_The_Aggregate_Is_Kept(
        bool parallel)
    {
        using ServiceProvider provider = BuildProvider(parallel: parallel);
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();
        var first = new InvalidOperationException("first");
        var second = new ArgumentException("second");
        var @event = new SeveralExceptionsEvent(first, second);

        EventPublishException exception = await Should.ThrowAsync<EventPublishException>(
            () => publisher.PublishAsync(@event));

        AggregateException aggregate = exception.Failures.ShouldHaveSingleItem().Exception
            .ShouldBeOfType<AggregateException>();
        aggregate.InnerExceptions.ShouldBe([first, second], ignoreOrder: false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Two_Contract_Handler_When_Both_Invocations_Fail_Then_Each_Failure_Keeps_Its_Contract_Identity(
        bool parallel)
    {
        using ServiceProvider provider = BuildProvider(parallel: parallel);
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();
        var exactFailure = new InvalidOperationException("exact");
        var baseFailure = new ArgumentException("base");
        var @event = new TwoContractDerivedEvent(exactFailure, baseFailure);

        EventPublishException exception = await Should.ThrowAsync<EventPublishException>(
            () => publisher.PublishAsync(@event));

        exception.Failures.Select(failure => failure.HandlerType).ShouldBe([
            typeof(TwoContractFailureHandler),
            typeof(TwoContractFailureHandler),
        ]);
        exception.Failures.Select(failure => failure.DeclaredEventType).ShouldBe([
            typeof(TwoContractDerivedEvent),
            typeof(TwoContractBaseEvent),
        ]);
        exception.Failures.Select(failure => failure.Exception).ShouldBe([
            exactFailure,
            baseFailure,
        ]);
    }

    [Fact]
    public async Task Given_A_Scoped_Two_Contract_Handler_When_Publishing_In_Parallel_Then_One_Instance_Is_Invoked_Concurrently()
    {
        using ServiceProvider provider = BuildProvider(parallel: true, scopedHandlers: true);
        using IServiceScope scope = provider.CreateScope();
        var resolutions = new List<string>();
        var services = new TimelineProvider(
            scope.ServiceProvider,
            resolutions,
            typeof(ScopedTwoContractHandler));
        var publisher = new EventPublisher(
            scope.ServiceProvider.GetRequiredService<EventMap>(),
            services);
        var @event = new ScopedTwoContractDerivedEvent();

        Task publish = publisher.PublishAsync(@event);

        @event.Calls.ShouldBe(["exact", "base"]);
        resolutions.ShouldBe([
            "resolve ScopedTwoContractHandler",
            "resolve ScopedTwoContractHandler",
        ]);
        @event.Instances.Count.ShouldBe(2);
        @event.Instances[0].ShouldBeSameAs(@event.Instances[1]);
        publish.IsCompleted.ShouldBeFalse();
        @event.ExactGate.SetResult(null);
        publish.IsCompleted.ShouldBeFalse();
        @event.BaseGate.SetResult(null);
        await publish;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_Concurrent_Publishes_When_Both_Fail_Then_Each_Task_Keeps_Only_Its_Own_Failure(
        bool parallel)
    {
        using ServiceProvider provider = BuildProvider(parallel: parallel);
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();
        var firstFailure = new InvalidOperationException("first");
        var secondFailure = new InvalidOperationException("second");
        var firstEvent = new ConcurrentFailureEvent();
        var secondEvent = new ConcurrentFailureEvent();

        Task firstPublish = publisher.PublishAsync(firstEvent);
        Task secondPublish = publisher.PublishAsync(secondEvent);
        firstEvent.Gate.SetException(firstFailure);
        secondEvent.Gate.SetException(secondFailure);
        EventPublishException firstException = await Should.ThrowAsync<EventPublishException>(
            () => firstPublish);
        EventPublishException secondException = await Should.ThrowAsync<EventPublishException>(
            () => secondPublish);

        firstException.Failures.ShouldHaveSingleItem().Exception.ShouldBeSameAs(firstFailure);
        secondException.Failures.ShouldHaveSingleItem().Exception.ShouldBeSameAs(secondFailure);
    }

    [Fact]
    public async Task Given_One_Assembly_Registered_Twice_When_Publishing_Then_The_Handler_Runs_Once()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options =>
            options.RegisterHandlersFromAssemblyContaining<EventPublisherTests>());
        services.AddRequestFlow(options =>
            options.RegisterHandlersFromAssemblyContaining<EventPublisherTests>());
        using ServiceProvider provider = services.BuildServiceProvider();
        var @event = new DuplicateAssemblyEvent();

        await provider.GetRequiredService<IEventPublisher>().PublishAsync(@event);

        @event.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_Fewer_Entries_Than_Handlers_When_Creating_A_Plan_Then_Throws_Argument_Exception(
        bool parallel)
    {
        var model = new EventModel(
            typeof(KnownEvent),
            [new EventHandlerModel(
                typeof(ConcurrentFailureHandler), typeof(KnownEvent), RequestFlowLifetime.Transient)]);

        Func<EventPlan> create = () => parallel
            ? new ParallelEventPlan(model, [])
            : new SequentialEventPlan(model, []);

        ArgumentException exception = Should.Throw<ArgumentException>(create);
        exception.ParamName.ShouldBe("entries");
    }

    #region Helpers

    private static async Task<Exception?> ExceptionFrom(Task task)
    {
        try
        {
            await task;
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static EventPublisher EmptyPublisher(
        Type eventType,
        IServiceProvider? services = null,
        bool parallel = false)
    {
        var model = new EventModel(eventType, []);
        var plans = new Dictionary<Type, EventPlan>
        {
            [eventType] = parallel
                ? new ParallelEventPlan(model, [])
                : new SequentialEventPlan(model, []),
        };

        return new EventPublisher(
            new EventMap(plans),
            services ?? Substitute.For<IServiceProvider>());
    }

    private static ServiceProvider BuildProvider(
        Action<IServiceCollection>? configure = null,
        bool parallel = false,
        bool scopedHandlers = false)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options =>
        {
            options.RegisterHandlersFromAssemblyContaining<EventPublisherTests>();
            if (parallel)
                options.PublishEventsInParallel();
            if (scopedHandlers)
                options.WithScopedHandlers();
        });
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private sealed class TimelineProvider(
        IServiceProvider inner,
        List<string> timeline,
        params Type[] recordedTypes) : IServiceProvider, ISupportRequiredService
    {
        private readonly HashSet<Type> _recordedTypes = [.. recordedTypes];

        public object? GetService(Type serviceType)
        {
            Record(serviceType);
            return inner.GetService(serviceType);
        }

        public object GetRequiredService(Type serviceType)
        {
            Record(serviceType);
            return inner.GetRequiredService(serviceType);
        }

        private void Record(Type serviceType)
        {
            if (_recordedTypes.Contains(serviceType))
                timeline.Add($"resolve {serviceType.Name}");
        }
    }

    public sealed record KnownEvent : IEvent;

    public sealed record UnknownEvent : IEvent;

    public record KnownBaseEvent : IEvent;

    public sealed record UnknownDerivedEvent : KnownBaseEvent;

    public sealed record ResolutionEvent(List<string> Timeline) : IEvent;

    public sealed class AlphaResolutionHandler : IEventHandler<ResolutionEvent>
    {
        public Task HandleAsync(ResolutionEvent @event, CancellationToken cancellationToken)
        {
            @event.Timeline.Add("handle AlphaResolutionHandler");
            return Task.CompletedTask;
        }
    }

    public sealed class ZuluResolutionHandler : IEventHandler<ResolutionEvent>
    {
        public Task HandleAsync(ResolutionEvent @event, CancellationToken cancellationToken)
        {
            @event.Timeline.Add("handle ZuluResolutionHandler");
            return Task.CompletedTask;
        }
    }

    public sealed record SynchronousThrowEvent(Exception Failure) : IEvent
    {
        public List<string> Calls { get; } = [];
    }

    public sealed class AlphaSynchronousThrowHandler : IEventHandler<SynchronousThrowEvent>
    {
        public Task HandleAsync(SynchronousThrowEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("throw");
            throw @event.Failure;
        }
    }

    public sealed class ZuluAfterSynchronousThrowHandler : IEventHandler<SynchronousThrowEvent>
    {
        public Task HandleAsync(SynchronousThrowEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("after");
            return Task.CompletedTask;
        }
    }

    public sealed record NullTaskEvent : IEvent
    {
        public List<string> Calls { get; } = [];
    }

    public sealed class AlphaNullTaskHandler : IEventHandler<NullTaskEvent>
    {
        public Task HandleAsync(NullTaskEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("null");
            return null!;
        }
    }

    public sealed class ZuluAfterNullTaskHandler : IEventHandler<NullTaskEvent>
    {
        public Task HandleAsync(NullTaskEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("after");
            return Task.CompletedTask;
        }
    }

    public sealed record UnresolvableEvent : IEvent
    {
        public List<string> Calls { get; } = [];
    }

    public sealed class AlphaUnresolvableHandler : IEventHandler<UnresolvableEvent>
    {
        public AlphaUnresolvableHandler(MissingDependency dependency)
            => Dependency = dependency;

        public MissingDependency Dependency { get; }

        public Task HandleAsync(UnresolvableEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class ZuluAfterUnresolvableHandler : IEventHandler<UnresolvableEvent>
    {
        public Task HandleAsync(UnresolvableEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("after");
            return Task.CompletedTask;
        }
    }

    public sealed class MissingDependency;

    public sealed record SeveralExceptionsEvent(Exception First, Exception Second) : IEvent
    {
        public Task? HandlerTask { get; set; }
    }

    public sealed class SeveralExceptionsHandler : IEventHandler<SeveralExceptionsEvent>
    {
        public Task HandleAsync(SeveralExceptionsEvent @event, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<object?>();
            completion.SetException([@event.First, @event.Second]);
            @event.HandlerTask = completion.Task;
            return completion.Task;
        }
    }

    public record TwoContractBaseEvent(Exception ExactFailure, Exception BaseFailure) : IEvent;

    public sealed record TwoContractDerivedEvent(Exception ExactFailure, Exception BaseFailure)
        : TwoContractBaseEvent(ExactFailure, BaseFailure);

    public sealed class TwoContractFailureHandler :
        IEventHandler<TwoContractDerivedEvent>,
        IEventHandler<TwoContractBaseEvent>
    {
        public Task HandleAsync(
            TwoContractDerivedEvent @event,
            CancellationToken cancellationToken)
            => Task.FromException(@event.ExactFailure);

        public Task HandleAsync(
            TwoContractBaseEvent @event,
            CancellationToken cancellationToken)
            => Task.FromException(@event.BaseFailure);
    }

    public record ScopedTwoContractBaseEvent : IEvent
    {
        public List<string> Calls { get; } = [];

        public List<ScopedTwoContractHandler> Instances { get; } = [];

        public TaskCompletionSource<object?> ExactGate { get; } = new();

        public TaskCompletionSource<object?> BaseGate { get; } = new();
    }

    public sealed record ScopedTwoContractDerivedEvent : ScopedTwoContractBaseEvent;

    public sealed class ScopedTwoContractHandler :
        IEventHandler<ScopedTwoContractDerivedEvent>,
        IEventHandler<ScopedTwoContractBaseEvent>
    {
        public Task HandleAsync(
            ScopedTwoContractDerivedEvent @event,
            CancellationToken cancellationToken)
        {
            @event.Calls.Add("exact");
            @event.Instances.Add(this);
            return @event.ExactGate.Task;
        }

        public Task HandleAsync(
            ScopedTwoContractBaseEvent @event,
            CancellationToken cancellationToken)
        {
            @event.Calls.Add("base");
            @event.Instances.Add(this);
            return @event.BaseGate.Task;
        }
    }

    public sealed record ConcurrentFailureEvent : IEvent
    {
        public TaskCompletionSource<object?> Gate { get; } = new();
    }

    public sealed class ConcurrentFailureHandler : IEventHandler<ConcurrentFailureEvent>
    {
        public Task HandleAsync(
            ConcurrentFailureEvent @event,
            CancellationToken cancellationToken)
            => @event.Gate.Task;
    }

    public sealed record DuplicateAssemblyEvent : IEvent
    {
        public int Calls { get; set; }
    }

    public sealed class DuplicateAssemblyHandler : IEventHandler<DuplicateAssemblyEvent>
    {
        public Task HandleAsync(DuplicateAssemblyEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls++;
            return Task.CompletedTask;
        }
    }

    #endregion
}
