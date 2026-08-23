using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class EventValidationModelTests
{
    [Fact]
    public void Given_Events_And_Subscriptions_When_Building_Then_Both_Event_Views_Are_Derived()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddEvent(typeof(FirstEvent))
            .AddEvent(typeof(SecondEvent))
            .AddEventHandler(
                typeof(BaseEventHandler), typeof(BaseEvent), RequestFlowLifetime.Singleton)
            .AddEventHandler(
                typeof(FirstEventHandler), typeof(FirstEvent), RequestFlowLifetime.Scoped)
            .Build();

        EventModel first = model.Events[0];
        first.EventType.ShouldBe(typeof(FirstEvent));
        first.Handlers.Select(handler => handler.HandlerType).ShouldBe([
            typeof(FirstEventHandler),
            typeof(BaseEventHandler),
        ]);
        first.Handlers.Select(handler => handler.DeclaredEventType).ShouldBe([
            typeof(FirstEvent),
            typeof(BaseEvent),
        ]);
        first.Handlers.Select(handler => handler.Lifetime).ShouldBe([
            RequestFlowLifetime.Scoped,
            RequestFlowLifetime.Singleton,
        ]);

        EventSubscriptionModel baseSubscription = model.EventSubscriptions[0];
        baseSubscription.HandlerType.ShouldBe(typeof(BaseEventHandler));
        baseSubscription.DeclaredEventType.ShouldBe(typeof(BaseEvent));
        baseSubscription.Lifetime.ShouldBe(RequestFlowLifetime.Singleton);
        baseSubscription.ReachedEvents.ShouldBe([typeof(FirstEvent), typeof(SecondEvent)]);

        EventSubscriptionModel exactSubscription = model.EventSubscriptions[1];
        exactSubscription.ReachedEvents.ShouldBe([typeof(FirstEvent)]);
    }

    [Fact]
    public void Given_A_Concrete_Closed_Declared_Event_When_Building_Then_It_Joins_After_Explicit_Events()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddEvent(typeof(FirstEvent))
            .AddEventHandler(typeof(ClosedGenericEventHandler), typeof(GenericEvent<int>))
            .Build();

        model.Events.Select(@event => @event.EventType).ShouldBe([
            typeof(FirstEvent),
            typeof(GenericEvent<int>),
        ]);
        model.Events[1].Handlers.ShouldHaveSingleItem().HandlerType
            .ShouldBe(typeof(ClosedGenericEventHandler));
    }

    [Fact]
    public void Given_Non_Concrete_Declared_Event_Types_When_Building_Then_They_Do_Not_Join_The_Event_Universe()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddEventHandler(typeof(UniversalEventHandler), typeof(IEvent))
            .AddEventHandler(typeof(AbstractEventHandler), typeof(AbstractEvent))
            .AddEventHandler(typeof(OpenGenericEventHandler), typeof(GenericEvent<>))
            .Build();

        model.Events.ShouldBeEmpty();
        model.EventSubscriptions.Count.ShouldBe(3);
        model.EventSubscriptions.ShouldAllBe(subscription => subscription.ReachedEvents.Count == 0);
    }

    [Fact]
    public void Given_The_Same_Event_Added_More_Than_Once_When_Building_Then_It_Appears_Once()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddEvent(typeof(FirstEvent))
            .AddEvent(typeof(FirstEvent))
            .AddEventHandler(typeof(FirstEventHandler), typeof(FirstEvent))
            .Build();

        model.Events.ShouldHaveSingleItem();
    }

    [Fact]
    public void Given_No_Events_When_Building_Then_Both_Event_Lists_Are_Empty()
    {
        RequestFlowModel model = new RequestFlowModelBuilder().Build();

        model.Events.ShouldBeEmpty();
        model.EventSubscriptions.ShouldBeEmpty();
        model.EventStrategies.ShouldBeEmpty();
    }

    [Fact]
    public void Given_No_Strategy_Declaration_When_Building_Then_Sequential_Is_The_Event_Default()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddEvent(typeof(FirstEvent))
            .Build();

        model.Events.ShouldHaveSingleItem().PublishStrategy
            .ShouldBe(typeof(SequentialPublishStrategy));
    }

    [Fact]
    public void Given_A_Global_Strategy_When_Building_Then_It_Is_The_Fallback_For_Every_Event()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddEvent(typeof(FirstEvent))
            .AddEvent(typeof(SecondEvent))
            .PublishEventsWith(null, typeof(GlobalStrategy))
            .Build();

        model.Events.ShouldAllBe(@event => @event.PublishStrategy == typeof(GlobalStrategy));
        EventStrategyModel strategy = model.EventStrategies.ShouldHaveSingleItem();
        strategy.DeclaredEventType.ShouldBeNull();
        strategy.StrategyType.ShouldBe(typeof(GlobalStrategy));
        strategy.Lifetime.ShouldBe(RequestFlowLifetime.Singleton);
        strategy.ReachedEvents.ShouldBe([typeof(FirstEvent), typeof(SecondEvent)]);
    }

    [Fact]
    public void Given_Exact_Interface_And_Global_Strategies_When_Building_Then_Exact_Wins()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddEvent(typeof(FirstEvent))
            .PublishEventsWith(null, typeof(GlobalStrategy))
            .PublishEventsWith(typeof(IEvent), typeof(CatchAllStrategy))
            .PublishEventsWith(typeof(FirstEvent), typeof(ExactStrategy))
            .Build();

        model.Events.ShouldHaveSingleItem().PublishStrategy.ShouldBe(typeof(ExactStrategy));
        model.EventStrategies[0].ReachedEvents.ShouldBeEmpty();
        model.EventStrategies[1].ReachedEvents.ShouldBeEmpty();
        model.EventStrategies[2].ReachedEvents.ShouldBe([typeof(FirstEvent)]);
    }

    [Fact]
    public void Given_Near_And_Far_Base_Strategies_When_Building_Then_The_Nearer_Base_Wins()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddEvent(typeof(LeafEvent))
            .PublishEventsWith(typeof(BaseEvent), typeof(FarBaseStrategy))
            .PublishEventsWith(typeof(MiddleEvent), typeof(NearBaseStrategy))
            .Build();

        model.Events.ShouldHaveSingleItem().PublishStrategy.ShouldBe(typeof(NearBaseStrategy));
    }

    [Fact]
    public void Given_An_Extending_Interface_When_Building_Then_Its_Strategy_Beats_The_Base_Interface()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddEvent(typeof(MultiInterfaceEvent))
            .PublishEventsWith(typeof(IBaseMarkerEvent), typeof(BaseInterfaceStrategy))
            .PublishEventsWith(typeof(IDerivedMarkerEvent), typeof(DerivedInterfaceStrategy))
            .Build();

        model.Events.ShouldHaveSingleItem().PublishStrategy
            .ShouldBe(typeof(DerivedInterfaceStrategy));
    }

    [Fact]
    public void Given_Two_Unrelated_Interface_Strategies_When_Building_Then_No_Winner_Is_Recorded()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddEvent(typeof(MultiInterfaceEvent))
            .PublishEventsWith(typeof(IDerivedMarkerEvent), typeof(DerivedInterfaceStrategy))
            .PublishEventsWith(typeof(IOtherMarkerEvent), typeof(OtherInterfaceStrategy))
            .Build();

        model.Events.ShouldHaveSingleItem().PublishStrategy.ShouldBeNull();
    }

    [Fact]
    public void Given_An_IEvent_Declaration_And_A_Global_When_Building_Then_The_IEvent_Declaration_Wins()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddEvent(typeof(FirstEvent))
            .PublishEventsWith(null, typeof(GlobalStrategy))
            .PublishEventsWith(typeof(IEvent), typeof(CatchAllStrategy))
            .Build();

        model.Events.ShouldHaveSingleItem().PublishStrategy.ShouldBe(typeof(CatchAllStrategy));
    }

    [Fact]
    public void Given_An_Event_Model_When_A_Rule_Casts_Its_Lists_Then_They_Reject_Mutation()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddEvent(typeof(FirstEvent))
            .AddEventHandler(typeof(FirstEventHandler), typeof(FirstEvent))
            .Build();

        Should.Throw<NotSupportedException>(() => ((IList<EventModel>)model.Events).Clear());
        Should.Throw<NotSupportedException>(
            () => ((IList<EventSubscriptionModel>)model.EventSubscriptions).Clear());
        Should.Throw<NotSupportedException>(
            () => ((IList<EventHandlerModel>)model.Events[0].Handlers).Clear());
        Should.Throw<NotSupportedException>(
            () => ((IList<Type>)model.EventSubscriptions[0].ReachedEvents).Clear());

        RequestFlowModel strategyModel = new RequestFlowModelBuilder()
            .AddEvent(typeof(FirstEvent))
            .PublishEventsWith(null, typeof(GlobalStrategy))
            .Build();

        Should.Throw<NotSupportedException>(
            () => ((IList<EventStrategyModel>)strategyModel.EventStrategies).Clear());
        Should.Throw<NotSupportedException>(
            () => ((IList<Type>)strategyModel.EventStrategies[0].ReachedEvents).Clear());
    }

    [Fact]
    public void Given_All_Four_Flags_When_Building_A_Context_Then_They_Are_Recorded()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder().BuildContext(
            unhandledRequestsAllowed: true,
            unusedStagesDisallowed: false,
            unhandledEventsAllowed: true,
            unusedEventHandlersDisallowed: true);

        context.UnhandledRequestsAllowed.ShouldBeTrue();
        context.UnusedStagesDisallowed.ShouldBeFalse();
        context.UnhandledEventsAllowed.ShouldBeTrue();
        context.UnusedEventHandlersDisallowed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(typeof(IMarkerEvent))]
    [InlineData(typeof(AbstractEvent))]
    [InlineData(typeof(GenericEvent<>))]
    [InlineData(typeof(NotAnEvent))]
    public void Given_A_Type_The_Model_Cannot_Hold_When_Adding_A_Known_Event_Then_Throws_Argument_Exception(
        Type eventType)
    {
        var sut = new RequestFlowModelBuilder();

        ArgumentException exception = Should.Throw<ArgumentException>(() => sut.AddEvent(eventType));

        exception.ParamName.ShouldBe("eventType");
    }

    [Fact]
    public void Given_A_Null_Event_Type_When_Adding_An_Event_Then_Throws_Argument_Null_Exception()
    {
        var sut = new RequestFlowModelBuilder();

        Should.Throw<ArgumentNullException>(() => sut.AddEvent(null!));
    }

    [Fact]
    public void Given_A_Null_Handler_Type_When_Adding_An_Event_Subscription_Then_Throws_Argument_Null_Exception()
    {
        var sut = new RequestFlowModelBuilder();

        Should.Throw<ArgumentNullException>(
            () => sut.AddEventHandler(null!, typeof(FirstEvent)));
    }

    [Theory]
    [InlineData(typeof(IDisposable))]
    [InlineData(typeof(NotAnEvent))]
    public void Given_A_Declared_Type_That_Is_No_Event_Contract_When_Adding_An_Event_Subscription_Then_Throws_Argument_Exception(
        Type declaredEventType)
    {
        var sut = new RequestFlowModelBuilder();

        ArgumentException exception = Should.Throw<ArgumentException>(
            () => sut.AddEventHandler(typeof(FirstEventHandler), declaredEventType));

        exception.ParamName.ShouldBe("declaredEventType");
    }

    [Fact]
    public void Given_A_Null_Declared_Event_Type_When_Adding_An_Event_Subscription_Then_Throws_Argument_Null_Exception()
    {
        var sut = new RequestFlowModelBuilder();

        Should.Throw<ArgumentNullException>(
            () => sut.AddEventHandler(typeof(FirstEventHandler), null!));
    }

    #region Helpers

    private abstract record BaseEvent : IEvent;

    private sealed record FirstEvent : BaseEvent;

    private sealed record SecondEvent : BaseEvent;

    private abstract record MiddleEvent : BaseEvent;

    private sealed record LeafEvent : MiddleEvent;

    private abstract record AbstractEvent : IEvent;

    private interface IMarkerEvent : IEvent
    { }

    private interface IBaseMarkerEvent : IEvent
    { }

    private interface IDerivedMarkerEvent : IBaseMarkerEvent
    { }

    private interface IOtherMarkerEvent : IEvent
    { }

    private sealed record MultiInterfaceEvent : IDerivedMarkerEvent, IOtherMarkerEvent;

    private sealed record NotAnEvent;

    private sealed record GenericEvent<T> : IEvent;

    private sealed class BaseEventHandler
    { }

    private sealed class FirstEventHandler
    { }

    private sealed class ClosedGenericEventHandler
    { }

    private sealed class UniversalEventHandler
    { }

    private sealed class AbstractEventHandler
    { }

    private sealed class OpenGenericEventHandler
    { }

    private abstract class Strategy : IEventPublishStrategy
    {
        public Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class GlobalStrategy : Strategy;

    private sealed class CatchAllStrategy : Strategy;

    private sealed class ExactStrategy : Strategy;

    private sealed class FarBaseStrategy : Strategy;

    private sealed class NearBaseStrategy : Strategy;

    private sealed class BaseInterfaceStrategy : Strategy;

    private sealed class DerivedInterfaceStrategy : Strategy;

    private sealed class OtherInterfaceStrategy : Strategy;

    #endregion
}
