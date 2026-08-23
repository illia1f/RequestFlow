using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Events;

public sealed class EventSemanticsTests
{
    [Fact]
    public async Task Given_A_Throwing_Synchronization_Context_When_A_Sequential_Handler_Suspends_Then_Publish_Returns_A_Task_Without_Using_Context_Hooks()
    {
        var @event = new SynchronizationContextEvent();
        EventPublisher publisher = DirectPublisherFor<SynchronizationContextEvent>(
            typeof(SynchronizationContextHandler),
            (_, value, _) => ((SynchronizationContextEvent)value).Gate.Task);
        var context = new ThrowingOperationSynchronizationContext();
        SynchronizationContext? previous = SynchronizationContext.Current;
        Task? publish = null;
        Exception? synchronousFailure = null;

        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                publish = publisher.PublishAsync(@event);
            }
            catch (Exception exception)
            {
                synchronousFailure = exception;
            }
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
            @event.Gate.TrySetResult(null);
        }

        synchronousFailure.ShouldBeNull();
        (publish is not null).ShouldBeTrue();
        await publish!;
        context.OperationStartedCalls.ShouldBe(0);
        context.OperationCompletedCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Pre_Canceled_Publishing_Token_When_Publishing_A_Nonempty_Plan_Then_No_Handler_Runs_And_Cancellation_Is_Acknowledged(
        bool parallel)
    {
        EventPublisher publisher = PublisherFor<PreCanceledEvent>(
            parallel,
            typeof(AlphaPreCanceledHandler),
            typeof(ZuluPreCanceledHandler));
        using var source = new CancellationTokenSource();
        source.Cancel();
        var @event = new PreCanceledEvent();

        Task publish = publisher.PublishAsync(@event, source.Token);
        EventPublishCanceledException exception = (await ExceptionFrom(publish))
            .ShouldBeOfType<EventPublishCanceledException>();

        @event.Calls.ShouldBeEmpty();
        exception.Failures.ShouldBeEmpty();
        exception.CancellationToken.ShouldBe(source.Token);
        exception.Message.ShouldContain("2 handlers were skipped");
        publish.IsCanceled.ShouldBeTrue();
        publish.Exception.ShouldBeNull();
    }

    [Fact]
    public async Task Given_Cancellation_Between_Sequential_Handlers_When_Publishing_Then_Remaining_Handlers_Are_Skipped()
    {
        EventPublisher publisher = PublisherFor<BetweenHandlersCancellationEvent>(
            parallel: false,
            typeof(AlphaBetweenHandlersCancellationHandler),
            typeof(BravoBetweenHandlersCancellationHandler),
            typeof(ZuluBetweenHandlersCancellationHandler));
        using var source = new CancellationTokenSource();
        var @event = new BetweenHandlersCancellationEvent(source);

        Task publish = publisher.PublishAsync(@event, source.Token);
        @event.Calls.ShouldBe(["first"]);
        source.Cancel();
        @event.Gate.SetResult(null);
        EventPublishCanceledException exception = (await ExceptionFrom(publish))
            .ShouldBeOfType<EventPublishCanceledException>();

        @event.Calls.ShouldBe(["first"]);
        exception.Failures.ShouldBeEmpty();
        exception.CancellationToken.ShouldBe(source.Token);
        exception.Message.ShouldContain("2 handlers were skipped");
        publish.IsCanceled.ShouldBeTrue();
        publish.Exception.ShouldBeNull();
    }

    [Fact]
    public async Task Given_A_Failure_Before_Sequential_Cancellation_When_Publishing_Then_The_Cancellation_Keeps_The_Failure()
    {
        EventPublisher publisher = PublisherFor<FailureBeforeCancellationEvent>(
            parallel: false,
            typeof(AlphaFailureBeforeCancellationHandler),
            typeof(ZuluFailureBeforeCancellationHandler));
        using var source = new CancellationTokenSource();
        var failure = new InvalidOperationException("failure");
        var @event = new FailureBeforeCancellationEvent(source, failure);

        Task publish = publisher.PublishAsync(@event, source.Token);
        publish.IsCompleted.ShouldBeFalse();
        @event.Gate.SetResult(null);
        EventPublishCanceledException exception = (await ExceptionFrom(publish))
            .ShouldBeOfType<EventPublishCanceledException>();

        @event.Calls.ShouldBe(["fail"]);
        exception.Failures.ShouldHaveSingleItem().Exception.ShouldBeSameAs(failure);
        exception.CancellationToken.ShouldBe(source.Token);
        exception.Message.ShouldContain("1 handlers were skipped");
        publish.IsCanceled.ShouldBeTrue();
        publish.Exception.ShouldBeNull();
    }

    [Fact]
    public async Task Given_Cancellation_During_The_Only_Sequential_Handler_When_Publishing_Then_The_Publish_Succeeds()
    {
        EventPublisher publisher = PublisherFor<OnlyHandlerCancellationEvent>(
            parallel: false,
            typeof(OnlyHandlerCancellationHandler));
        using var source = new CancellationTokenSource();
        var @event = new OnlyHandlerCancellationEvent(source);

        await publisher.PublishAsync(@event, source.Token);

        @event.Calls.ShouldBe(["only"]);
        source.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task Given_Cancellation_During_The_Final_Sequential_Handler_When_Publishing_Then_The_Publish_Succeeds()
    {
        EventPublisher publisher = PublisherFor<FinalHandlerCancellationEvent>(
            parallel: false,
            typeof(AlphaBeforeFinalHandlerCancellationHandler),
            typeof(ZuluFinalHandlerCancellationHandler));
        using var source = new CancellationTokenSource();
        var @event = new FinalHandlerCancellationEvent(source);

        Task publish = publisher.PublishAsync(@event, source.Token);
        publish.IsCompleted.ShouldBeFalse();
        source.Cancel();
        @event.Gate.SetResult(null);
        await publish;

        @event.Calls.ShouldBe(["before", "final"]);
        source.IsCancellationRequested.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Given_A_Handler_Thrown_Operation_Canceled_Exception_When_Publishing_Then_It_Is_An_Ordinary_Failure_And_The_Walk_Continues(
        bool parallel,
        bool synchronous)
    {
        using ServiceProvider provider = BuildProvider(parallel);
        var failure = new OperationCanceledException("handler cancellation");
        var @event = new HandlerCancellationEvent(failure, synchronous);

        EventPublishException exception = await Should.ThrowAsync<EventPublishException>(() =>
            provider.GetRequiredService<IEventPublisher>().PublishAsync(@event));

        @event.Calls.ShouldBe(["cancel", "after"]);
        EventHandlerFailure handlerFailure = exception.Failures.ShouldHaveSingleItem();
        handlerFailure.HandlerType.ShouldBe(typeof(AlphaHandlerCancellationHandler));
        handlerFailure.DeclaredEventType.ShouldBe(typeof(HandlerCancellationEvent));
        handlerFailure.Exception.ShouldBeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task Given_One_Faulted_And_One_Canceled_Parallel_Entry_When_Publishing_Then_Both_Are_Failures_In_Plan_Order()
    {
        using ServiceProvider provider = BuildProvider(parallel: true);
        using var source = new CancellationTokenSource();
        source.Cancel();
        var failure = new InvalidOperationException("faulted");
        var @event = new ParallelFaultedAndCanceledEvent(failure, source.Token);

        EventPublishException exception = await Should.ThrowAsync<EventPublishException>(() =>
            provider.GetRequiredService<IEventPublisher>().PublishAsync(@event));

        @event.Calls.ShouldBe(["faulted", "canceled"]);
        exception.Failures.Select(item => item.HandlerType).ShouldBe([
            typeof(AlphaParallelFaultedHandler),
            typeof(ZuluParallelCanceledHandler),
        ]);
        exception.Failures[0].Exception.ShouldBeSameAs(failure);
        exception.Failures[1].Exception.ShouldBeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task Given_Parallel_Handlers_Fail_In_Reverse_Order_When_Publishing_Then_Failures_Keep_Plan_Order()
    {
        using ServiceProvider provider = BuildProvider(parallel: true);
        var alpha = new InvalidOperationException("alpha");
        var zulu = new ArgumentException("zulu");
        var @event = new ParallelFailureOrderEvent();

        Task publish = provider.GetRequiredService<IEventPublisher>().PublishAsync(@event);
        @event.Calls.ShouldBe(["alpha", "zulu"]);
        @event.ZuluGate.SetException(zulu);
        @event.AlphaGate.SetException(alpha);
        EventPublishException exception = await Should.ThrowAsync<EventPublishException>(() => publish);

        exception.Failures.Select(item => item.HandlerType).ShouldBe([
            typeof(AlphaParallelFailureOrderHandler),
            typeof(ZuluParallelFailureOrderHandler),
        ]);
        exception.Failures.Select(item => item.Exception).ShouldBe([alpha, zulu]);
    }

    [Fact]
    public async Task Given_A_Parallel_Publish_Token_Cancels_Mid_Flight_When_All_Handlers_Succeed_Then_The_Publish_Finishes_Successfully()
    {
        using ServiceProvider provider = BuildProvider(parallel: true);
        using var source = new CancellationTokenSource();
        var @event = new MidFlightParallelCancellationEvent();
        int publishingThread = Environment.CurrentManagedThreadId;

        Task publish = provider.GetRequiredService<IEventPublisher>()
            .PublishAsync(@event, source.Token);
        @event.Calls.ShouldBe(["alpha", "zulu"]);
        @event.StartThreads.ShouldBe([publishingThread, publishingThread]);
        publish.IsCompleted.ShouldBeFalse();
        source.Cancel();
        @event.AlphaGate.SetResult(null);
        publish.IsCompleted.ShouldBeFalse();
        @event.ZuluGate.SetResult(null);

        await publish;
    }

    [Fact]
    public async Task Given_A_Parallel_Publish_Token_Cancels_Mid_Flight_When_A_Handler_Fails_Then_The_Publish_Reports_The_Failure_After_All_Handlers_Finish()
    {
        using ServiceProvider provider = BuildProvider(parallel: true);
        using var source = new CancellationTokenSource();
        var failure = new InvalidOperationException("failure");
        var @event = new MidFlightParallelCancellationEvent();

        Task publish = provider.GetRequiredService<IEventPublisher>()
            .PublishAsync(@event, source.Token);
        source.Cancel();
        @event.AlphaGate.SetException(failure);
        publish.IsCompleted.ShouldBeFalse();
        @event.ZuluGate.SetResult(null);
        EventPublishException exception = await Should.ThrowAsync<EventPublishException>(() => publish);

        exception.Failures.ShouldHaveSingleItem().Exception.ShouldBeSameAs(failure);
    }

    [Fact]
    public async Task Given_One_Handler_Failure_When_Publishing_Sequentially_Then_The_Walk_Continues_And_Still_Aggregates()
    {
        using ServiceProvider provider = BuildProvider();
        var @event = new SingleFailureEvent(new InvalidOperationException("failure"));

        EventPublishException exception = await Should.ThrowAsync<EventPublishException>(() =>
            provider.GetRequiredService<IEventPublisher>().PublishAsync(@event));

        @event.Calls.ShouldBe(["fail", "after"]);
        exception.Failures.ShouldHaveSingleItem().Exception.ShouldBeSameAs(@event.Failure);
    }

    [Fact]
    public async Task Given_Several_Handler_Failures_When_Publishing_Sequentially_Then_Failures_Keep_Plan_Order()
    {
        using ServiceProvider provider = BuildProvider();
        var alpha = new InvalidOperationException("alpha");
        var zulu = new ArgumentException("zulu");
        var @event = new OrderedFailuresEvent(alpha, zulu);

        EventPublishException exception = await Should.ThrowAsync<EventPublishException>(() =>
            provider.GetRequiredService<IEventPublisher>().PublishAsync(@event));

        @event.Calls.ShouldBe(["alpha", "middle", "zulu"]);
        exception.Failures.Select(failure => failure.HandlerType).ShouldBe([
            typeof(AlphaOrderedFailureHandler),
            typeof(ZuluOrderedFailureHandler),
        ]);
        exception.Failures.Select(failure => failure.Exception).ShouldBe([alpha, zulu]);
    }

    [Fact]
    public async Task Given_A_Handler_Canceled_Task_When_Publishing_Sequentially_Then_It_Is_A_Failure_And_The_Walk_Continues()
    {
        using ServiceProvider provider = BuildProvider();
        using var source = new CancellationTokenSource();
        source.Cancel();
        var @event = new HandlerCanceledEvent(source.Token);

        EventPublishException exception = await Should.ThrowAsync<EventPublishException>(() =>
            provider.GetRequiredService<IEventPublisher>().PublishAsync(@event));

        @event.Calls.ShouldBe(["cancel", "after"]);
        exception.Failures.ShouldHaveSingleItem().Exception
            .ShouldBeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public void Given_All_Specificity_Tiers_When_Building_The_Closure_Then_Handlers_Have_The_Frozen_Order()
    {
        EventClosureResult result = EventClosure.Build(
            [typeof(DerivedEvent)],
            [
                Subscription<UniversalHandler, IEvent>(),
                Subscription<NarrowInterfaceHandler, INarrowEvent>(),
                Subscription<RootClassHandler, RootClassEvent>(),
                Subscription<ZuluExactHandler, DerivedEvent>(),
                Subscription<WideInterfaceHandler, IWideEvent>(),
                Subscription<BaseClassHandler, BaseEvent>(),
                Subscription<AlphaExactHandler, DerivedEvent>(),
            ]);

        EventHandlerModel[] handlers = [.. result.Events.ShouldHaveSingleItem().Handlers];

        handlers.Select(handler => handler.HandlerType).ShouldBe([
            typeof(AlphaExactHandler),
            typeof(ZuluExactHandler),
            typeof(BaseClassHandler),
            typeof(RootClassHandler),
            typeof(WideInterfaceHandler),
            typeof(NarrowInterfaceHandler),
            typeof(UniversalHandler),
        ]);
    }

    [Fact]
    public void Given_Diamond_And_Unrelated_Interfaces_When_Building_The_Closure_Then_Interface_Ties_Use_Ordinal_Keys()
    {
        EventClosureResult result = EventClosure.Build(
            [typeof(DiamondEvent)],
            [
                Subscription<UniversalHandler, IEvent>(),
                Subscription<TwoContractHandler, IUnrelatedEvent>(),
                Subscription<ZuluBranchHandler, ILeftEvent>(),
                Subscription<TwoContractHandler, IRootEvent>(),
                Subscription<AlphaBranchHandler, IRightEvent>(),
                Subscription<DiamondHandler, IDiamondEvent>(),
            ]);

        EventHandlerModel[] handlers = [.. result.Events.ShouldHaveSingleItem().Handlers];

        handlers.Select(handler => handler.HandlerType).ShouldBe([
            typeof(DiamondHandler),
            typeof(AlphaBranchHandler),
            typeof(ZuluBranchHandler),
            typeof(TwoContractHandler),
            typeof(TwoContractHandler),
            typeof(UniversalHandler),
        ]);
        handlers.Select(handler => handler.DeclaredEventType).ShouldBe([
            typeof(IDiamondEvent),
            typeof(IRightEvent),
            typeof(ILeftEvent),
            typeof(IRootEvent),
            typeof(IUnrelatedEvent),
            typeof(IEvent),
        ]);
    }

    [Fact]
    public void Given_A_Struct_Event_When_Building_The_Closure_Then_Exact_Interface_And_Universal_Tiers_Are_Ordered()
    {
        EventClosureResult result = EventClosure.Build(
            [typeof(StructEvent)],
            [
                Subscription<UniversalHandler, IEvent>(),
                Subscription<StructMarkerHandler, IStructMarker>(),
                Subscription<StructExactHandler, StructEvent>(),
            ]);

        result.Events.ShouldHaveSingleItem().Handlers.Select(handler => handler.HandlerType).ShouldBe([
            typeof(StructExactHandler),
            typeof(StructMarkerHandler),
            typeof(UniversalHandler),
        ]);
    }

    #region Helpers

    private static EventPublisher DirectPublisherFor<TEvent>(Type handlerType, HandlerEntry entry)
        where TEvent : IEvent
    {
        var model = new EventModel(typeof(TEvent), [
            new EventHandlerModel(
                handlerType,
                typeof(TEvent),
                RequestFlowLifetime.Transient),
        ]);
        var plan = new SequentialEventPlan(model, [entry]);
        var map = new EventMap(new Dictionary<Type, EventPlan>
        {
            [typeof(TEvent)] = plan,
        });

        return new EventPublisher(map, Substitute.For<IServiceProvider>());
    }

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

    private static EventPublisher PublisherFor<TEvent>(bool parallel, params Type[] handlerTypes)
        where TEvent : IEvent
    {
        var handlers = new EventHandlerModel[handlerTypes.Length];
        var entries = new HandlerEntry[handlerTypes.Length];

        for (int i = 0; i < handlerTypes.Length; i++)
        {
            Type handlerType = handlerTypes[i];
            handlers[i] = new EventHandlerModel(
                handlerType,
                typeof(TEvent),
                RequestFlowLifetime.Transient);
            entries[i] = (_, @event, cancellationToken) =>
            {
                var handler = (IEventHandler<TEvent>)Activator.CreateInstance(handlerType)!;
                return handler.HandleAsync((TEvent)@event, cancellationToken);
            };
        }

        var model = new EventModel(typeof(TEvent), handlers);
        EventPlan plan = parallel
            ? new ParallelEventPlan(model, entries)
            : new SequentialEventPlan(model, entries);
        var map = new EventMap(new Dictionary<Type, EventPlan>
        {
            [typeof(TEvent)] = plan,
        });

        return new EventPublisher(map, Substitute.For<IServiceProvider>());
    }

    private static ServiceProvider BuildProvider(bool parallel = false)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options =>
        {
            options.RegisterHandlersFromAssemblyContaining<EventSemanticsTests>();
            if (parallel)
                options.PublishEventsInParallel();
        });
        return services.BuildServiceProvider();
    }

    private static EventSubscriptionInput Subscription<THandler, TDeclared>()
        where TDeclared : IEvent
        => new(typeof(THandler), typeof(TDeclared), RequestFlowLifetime.Transient);

    private abstract record RootClassEvent : IEvent;

    private abstract record BaseEvent : RootClassEvent;

    private sealed record DerivedEvent : BaseEvent, IWideEvent;

    private interface INarrowEvent : IEvent;

    private interface IWideEvent : INarrowEvent;

    private interface IRootEvent : IEvent;

    private interface ILeftEvent : IRootEvent;

    private interface IRightEvent : IRootEvent;

    private interface IDiamondEvent : ILeftEvent, IRightEvent;

    private interface IUnrelatedEvent : IEvent;

    private sealed record DiamondEvent : IDiamondEvent, IUnrelatedEvent;

    private interface IStructMarker : IEvent;

    private readonly record struct StructEvent : IStructMarker;

    private sealed class AlphaExactHandler
    { }

    private sealed class ZuluExactHandler
    { }

    private sealed class BaseClassHandler
    { }

    private sealed class RootClassHandler
    { }

    private sealed class WideInterfaceHandler
    { }

    private sealed class NarrowInterfaceHandler
    { }

    private sealed class UniversalHandler
    { }

    private sealed class DiamondHandler
    { }

    private sealed class AlphaBranchHandler
    { }

    private sealed class ZuluBranchHandler
    { }

    private sealed class TwoContractHandler :
        IEventHandler<IRootEvent>,
        IEventHandler<IUnrelatedEvent>
    {
        public Task HandleAsync(IRootEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task HandleAsync(IUnrelatedEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class StructExactHandler
    { }

    private sealed class StructMarkerHandler
    { }

    public sealed record SingleFailureEvent(Exception Failure) : IEvent
    {
        public List<string> Calls { get; } = [];
    }

    public sealed class AlphaSingleFailureHandler : IEventHandler<SingleFailureEvent>
    {
        public Task HandleAsync(SingleFailureEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("fail");
            return Task.FromException(@event.Failure);
        }
    }

    public sealed class ZuluAfterSingleFailureHandler : IEventHandler<SingleFailureEvent>
    {
        public Task HandleAsync(SingleFailureEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("after");
            return Task.CompletedTask;
        }
    }

    public sealed record OrderedFailuresEvent(Exception Alpha, Exception Zulu) : IEvent
    {
        public List<string> Calls { get; } = [];
    }

    public sealed class AlphaOrderedFailureHandler : IEventHandler<OrderedFailuresEvent>
    {
        public Task HandleAsync(OrderedFailuresEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("alpha");
            return Task.FromException(@event.Alpha);
        }
    }

    public sealed class MiddleOrderedSuccessHandler : IEventHandler<OrderedFailuresEvent>
    {
        public Task HandleAsync(OrderedFailuresEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("middle");
            return Task.CompletedTask;
        }
    }

    public sealed class ZuluOrderedFailureHandler : IEventHandler<OrderedFailuresEvent>
    {
        public Task HandleAsync(OrderedFailuresEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("zulu");
            return Task.FromException(@event.Zulu);
        }
    }

    public sealed record HandlerCanceledEvent(CancellationToken HandlerToken) : IEvent
    {
        public List<string> Calls { get; } = [];
    }

    public sealed class AlphaHandlerCanceledHandler : IEventHandler<HandlerCanceledEvent>
    {
        public Task HandleAsync(HandlerCanceledEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("cancel");
            return Task.FromCanceled(@event.HandlerToken);
        }
    }

    public sealed class ZuluAfterHandlerCanceledHandler : IEventHandler<HandlerCanceledEvent>
    {
        public Task HandleAsync(HandlerCanceledEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("after");
            return Task.CompletedTask;
        }
    }

    public sealed record PreCanceledEvent : IEvent
    {
        public List<string> Calls { get; } = [];
    }

    public sealed class AlphaPreCanceledHandler : IEventHandler<PreCanceledEvent>
    {
        public Task HandleAsync(PreCanceledEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("alpha");
            return Task.CompletedTask;
        }
    }

    public sealed class ZuluPreCanceledHandler : IEventHandler<PreCanceledEvent>
    {
        public Task HandleAsync(PreCanceledEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("zulu");
            return Task.CompletedTask;
        }
    }

    public sealed record BetweenHandlersCancellationEvent(CancellationTokenSource Source) : IEvent
    {
        public List<string> Calls { get; } = [];

        public TaskCompletionSource<object?> Gate { get; } = new();
    }

    public sealed class AlphaBetweenHandlersCancellationHandler
        : IEventHandler<BetweenHandlersCancellationEvent>
    {
        public Task HandleAsync(
            BetweenHandlersCancellationEvent @event,
            CancellationToken cancellationToken)
        {
            @event.Calls.Add("first");
            return @event.Gate.Task;
        }
    }

    public sealed class BravoBetweenHandlersCancellationHandler
        : IEventHandler<BetweenHandlersCancellationEvent>
    {
        public Task HandleAsync(
            BetweenHandlersCancellationEvent @event,
            CancellationToken cancellationToken)
        {
            @event.Calls.Add("bravo");
            return Task.CompletedTask;
        }
    }

    public sealed class ZuluBetweenHandlersCancellationHandler
        : IEventHandler<BetweenHandlersCancellationEvent>
    {
        public Task HandleAsync(
            BetweenHandlersCancellationEvent @event,
            CancellationToken cancellationToken)
        {
            @event.Calls.Add("zulu");
            return Task.CompletedTask;
        }
    }

    public sealed record FailureBeforeCancellationEvent(
        CancellationTokenSource Source,
        Exception Failure) : IEvent
    {
        public List<string> Calls { get; } = [];

        public TaskCompletionSource<object?> Gate { get; } = new();
    }

    public sealed class AlphaFailureBeforeCancellationHandler
        : IEventHandler<FailureBeforeCancellationEvent>
    {
        public async Task HandleAsync(
            FailureBeforeCancellationEvent @event,
            CancellationToken cancellationToken)
        {
            @event.Calls.Add("fail");
            await @event.Gate.Task.ConfigureAwait(false);
            @event.Source.Cancel();
            throw @event.Failure;
        }
    }

    public sealed class ZuluFailureBeforeCancellationHandler
        : IEventHandler<FailureBeforeCancellationEvent>
    {
        public Task HandleAsync(
            FailureBeforeCancellationEvent @event,
            CancellationToken cancellationToken)
        {
            @event.Calls.Add("after");
            return Task.CompletedTask;
        }
    }

    public sealed record OnlyHandlerCancellationEvent(CancellationTokenSource Source) : IEvent
    {
        public List<string> Calls { get; } = [];
    }

    public sealed class OnlyHandlerCancellationHandler : IEventHandler<OnlyHandlerCancellationEvent>
    {
        public Task HandleAsync(
            OnlyHandlerCancellationEvent @event,
            CancellationToken cancellationToken)
        {
            @event.Calls.Add("only");
            @event.Source.Cancel();
            return Task.CompletedTask;
        }
    }

    public sealed record FinalHandlerCancellationEvent(CancellationTokenSource Source) : IEvent
    {
        public List<string> Calls { get; } = [];

        public TaskCompletionSource<object?> Gate { get; } = new();
    }

    public sealed class AlphaBeforeFinalHandlerCancellationHandler
        : IEventHandler<FinalHandlerCancellationEvent>
    {
        public Task HandleAsync(
            FinalHandlerCancellationEvent @event,
            CancellationToken cancellationToken)
        {
            @event.Calls.Add("before");
            return Task.CompletedTask;
        }
    }

    public sealed class ZuluFinalHandlerCancellationHandler
        : IEventHandler<FinalHandlerCancellationEvent>
    {
        public async Task HandleAsync(
            FinalHandlerCancellationEvent @event,
            CancellationToken cancellationToken)
        {
            @event.Calls.Add("final");
            await @event.Gate.Task.ConfigureAwait(false);
        }
    }

    public sealed record SynchronizationContextEvent : IEvent
    {
        public TaskCompletionSource<object?> Gate { get; } = new();
    }

    public sealed class SynchronizationContextHandler;

    private sealed class ThrowingOperationSynchronizationContext : SynchronizationContext
    {
        public int OperationStartedCalls { get; private set; }

        public int OperationCompletedCalls { get; private set; }

        public override void OperationStarted()
        {
            OperationStartedCalls++;
            throw new InvalidOperationException("OperationStarted must not be called.");
        }

        public override void OperationCompleted()
        {
            OperationCompletedCalls++;
            throw new InvalidOperationException("OperationCompleted must not be called.");
        }
    }

    public sealed record HandlerCancellationEvent(
        OperationCanceledException Failure,
        bool Synchronous) : IEvent
    {
        public List<string> Calls { get; } = [];
    }

    public sealed class AlphaHandlerCancellationHandler : IEventHandler<HandlerCancellationEvent>
    {
        public Task HandleAsync(
            HandlerCancellationEvent @event,
            CancellationToken cancellationToken)
        {
            @event.Calls.Add("cancel");
            if (@event.Synchronous)
                throw @event.Failure;

            return ThrowAsync(@event.Failure);
        }

        private static async Task ThrowAsync(OperationCanceledException failure)
        {
            await Task.Yield();
            throw failure;
        }
    }

    public sealed class ZuluAfterHandlerCancellationHandler
        : IEventHandler<HandlerCancellationEvent>
    {
        public Task HandleAsync(
            HandlerCancellationEvent @event,
            CancellationToken cancellationToken)
        {
            @event.Calls.Add("after");
            return Task.CompletedTask;
        }
    }

    public sealed record ParallelFaultedAndCanceledEvent(
        Exception Failure,
        CancellationToken HandlerToken) : IEvent
    {
        public List<string> Calls { get; } = [];
    }

    public sealed class AlphaParallelFaultedHandler
        : IEventHandler<ParallelFaultedAndCanceledEvent>
    {
        public Task HandleAsync(
            ParallelFaultedAndCanceledEvent @event,
            CancellationToken cancellationToken)
        {
            @event.Calls.Add("faulted");
            return Task.FromException(@event.Failure);
        }
    }

    public sealed class ZuluParallelCanceledHandler
        : IEventHandler<ParallelFaultedAndCanceledEvent>
    {
        public Task HandleAsync(
            ParallelFaultedAndCanceledEvent @event,
            CancellationToken cancellationToken)
        {
            @event.Calls.Add("canceled");
            return Task.FromCanceled(@event.HandlerToken);
        }
    }

    public sealed record ParallelFailureOrderEvent : IEvent
    {
        public List<string> Calls { get; } = [];

        public TaskCompletionSource<object?> AlphaGate { get; } = new();

        public TaskCompletionSource<object?> ZuluGate { get; } = new();
    }

    public sealed class AlphaParallelFailureOrderHandler
        : IEventHandler<ParallelFailureOrderEvent>
    {
        public Task HandleAsync(
            ParallelFailureOrderEvent @event,
            CancellationToken cancellationToken)
        {
            @event.Calls.Add("alpha");
            return @event.AlphaGate.Task;
        }
    }

    public sealed class ZuluParallelFailureOrderHandler
        : IEventHandler<ParallelFailureOrderEvent>
    {
        public Task HandleAsync(
            ParallelFailureOrderEvent @event,
            CancellationToken cancellationToken)
        {
            @event.Calls.Add("zulu");
            return @event.ZuluGate.Task;
        }
    }

    public sealed record MidFlightParallelCancellationEvent : IEvent
    {
        public List<string> Calls { get; } = [];

        public List<int> StartThreads { get; } = [];

        public TaskCompletionSource<object?> AlphaGate { get; } = new();

        public TaskCompletionSource<object?> ZuluGate { get; } = new();
    }

    public sealed class AlphaMidFlightParallelCancellationHandler
        : IEventHandler<MidFlightParallelCancellationEvent>
    {
        public Task HandleAsync(
            MidFlightParallelCancellationEvent @event,
            CancellationToken cancellationToken)
        {
            @event.Calls.Add("alpha");
            @event.StartThreads.Add(Environment.CurrentManagedThreadId);
            return @event.AlphaGate.Task;
        }
    }

    public sealed class ZuluMidFlightParallelCancellationHandler
        : IEventHandler<MidFlightParallelCancellationEvent>
    {
        public Task HandleAsync(
            MidFlightParallelCancellationEvent @event,
            CancellationToken cancellationToken)
        {
            @event.Calls.Add("zulu");
            @event.StartThreads.Add(Environment.CurrentManagedThreadId);
            return @event.ZuluGate.Task;
        }
    }

    #endregion
}
