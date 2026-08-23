using RequestFlow;

namespace RequestFlow.Tests.Unit.Events;

public sealed class EventDeliveryTests
{
    [Fact]
    public async Task Given_A_Delivery_Over_An_Entry_When_Starting_Then_Exposes_The_Entry_And_Returns_Its_Failure()
    {
        var @event = new TestEvent();
        var cause = new InvalidOperationException("failure");
        var failure = new EventHandlerFailure(
            typeof(TestHandler), typeof(TestEvent), cause);
        using var source = new CancellationTokenSource();
        CancellationToken publishingToken = source.Token;
        var subscription = new EventSubscription(typeof(TestHandler), typeof(TestEvent));
        int observedIndex = -1;
        CancellationToken observedToken = default;
        EventDelivery delivery = EventDelivery.Over(
            @event,
            [subscription],
            (index, cancellationToken) =>
            {
                observedIndex = index;
                observedToken = cancellationToken;
                return Task.FromResult<EventHandlerFailure?>(failure);
            },
            cancellationToken: publishingToken);

        EventHandlerFailure? result = await delivery.StartAsync(0);

        delivery.Event.ShouldBeSameAs(@event);
        delivery.EventType.ShouldBe(typeof(TestEvent));
        delivery.Count.ShouldBe(1);
        delivery[0].HandlerType.ShouldBe(typeof(TestHandler));
        delivery[0].DeclaredEventType.ShouldBe(typeof(TestEvent));
        observedIndex.ShouldBe(0);
        observedToken.ShouldBe(publishingToken);
        result.ShouldBeSameAs(failure);
    }

    [Fact]
    public void Given_A_Default_Delivery_When_Starting_An_Entry_Then_Throws_The_Over_Guard()
    {
        var delivery = default(EventDelivery);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => delivery.StartAsync(0));

        exception.Message.ShouldContain("EventDelivery.Over");
    }

    [Fact]
    public void Given_A_Default_Delivery_When_Reading_The_Event_Then_Throws_The_Over_Guard()
    {
        var delivery = default(EventDelivery);

        Should.Throw<InvalidOperationException>(() => _ = delivery.Event)
            .Message.ShouldContain("EventDelivery.Over");
        Should.Throw<InvalidOperationException>(() => _ = delivery.EventType)
            .Message.ShouldContain("EventDelivery.Over");
    }

    [Fact]
    public void Given_A_Default_Delivery_When_Ending_The_Publish_Then_Throws_The_Over_Guard()
    {
        var delivery = default(EventDelivery);
        var failure = new EventHandlerFailure(
            typeof(TestHandler), typeof(TestEvent), new InvalidOperationException("failure"));

        Should.Throw<InvalidOperationException>(() => delivery.ThrowIfAny([failure]))
            .Message.ShouldContain("EventDelivery.Over");
        Should.Throw<InvalidOperationException>(
                () => _ = delivery.Canceled([failure], skippedHandlerCount: 0, CancellationToken.None))
            .Message.ShouldContain("EventDelivery.Over");
    }

    [Fact]
    public void Given_A_Default_Delivery_When_Reading_The_Count_Then_It_Reports_No_Entries()
    {
        default(EventDelivery).Count.ShouldBe(0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void Given_An_Index_Outside_The_Delivery_When_Starting_Then_Throws_Argument_Out_Of_Range(
        int index)
    {
        EventDelivery delivery = SuccessfulDelivery();

        ArgumentOutOfRangeException exception = Should.Throw<ArgumentOutOfRangeException>(
            () => delivery.StartAsync(index));

        exception.ParamName.ShouldBe("index");
    }

    [Fact]
    public async Task Given_A_Delivery_Over_A_Failing_Entry_When_Running_Then_The_Handler_Exception_Is_Rethrown()
    {
        var cause = new InvalidOperationException("failure");
        EventDelivery delivery = DeliveryReturning(new EventHandlerFailure(
            typeof(TestHandler), typeof(TestEvent), cause));

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => delivery.RunAsync(0));

        exception.ShouldBeSameAs(cause);
    }

    [Fact]
    public async Task Given_A_Delivery_Over_A_Successful_Entry_When_Running_Then_Completes()
    {
        EventDelivery delivery = SuccessfulDelivery();

        await delivery.RunAsync(0);
    }

    [Fact]
    public async Task Given_An_Explicit_Entry_Token_When_The_Publishing_Token_Is_Canceled_Then_The_Entry_Remains_Detached()
    {
        using var publishingSource = new CancellationTokenSource();
        using var entrySource = new CancellationTokenSource();
        var gate = new TaskCompletionSource<EventHandlerFailure?>();
        CancellationToken observedToken = default;
        EventDelivery delivery = EventDelivery.Over(
            new TestEvent(),
            [new EventSubscription(typeof(TestHandler), typeof(TestEvent))],
            (_, cancellationToken) =>
            {
                observedToken = cancellationToken;
                return gate.Task;
            },
            cancellationToken: publishingSource.Token);

        Task<EventHandlerFailure?> entry = delivery.StartAsync(0, entrySource.Token);
        publishingSource.Cancel();

        observedToken.ShouldBe(entrySource.Token);
        observedToken.IsCancellationRequested.ShouldBeFalse();
        entry.IsCompleted.ShouldBeFalse();
        gate.SetResult(null);
        await entry;
    }

    [Fact]
    public async Task Given_None_As_The_Entry_Token_When_Starting_Then_It_Uses_The_Publishing_Token()
    {
        using var source = new CancellationTokenSource();
        CancellationToken observedToken = default;
        EventDelivery delivery = EventDelivery.Over(
            new TestEvent(),
            [new EventSubscription(typeof(TestHandler), typeof(TestEvent))],
            (_, cancellationToken) =>
            {
                observedToken = cancellationToken;
                return Task.FromResult<EventHandlerFailure?>(null);
            },
            cancellationToken: source.Token);

        await delivery.StartAsync(0, CancellationToken.None);
        source.Cancel();

        observedToken.ShouldBe(source.Token);
        observedToken.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public void Given_A_Delivery_With_A_Provider_When_Reading_Services_Then_Returns_The_Provider()
    {
        var services = Substitute.For<IServiceProvider>();
        EventDelivery delivery = EventDelivery.Over(
            new TestEvent(),
            [],
            (_, _) => Task.FromResult<EventHandlerFailure?>(null),
            services);

        delivery.Services.ShouldBeSameAs(services);
    }

    [Fact]
    public void Given_A_Delivery_Without_A_Provider_When_Reading_Services_Then_Throws_The_Over_Parameter_Guard()
    {
        EventDelivery delivery = EventDelivery.Over(
            new TestEvent(),
            [],
            (_, _) => Task.FromResult<EventHandlerFailure?>(null));

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => _ = delivery.Services);

        exception.Message.ShouldContain("EventDelivery.Over");
        exception.Message.ShouldContain("services");
    }

    [Fact]
    public void Given_No_Failures_When_Using_The_Standard_Ending_Then_It_Does_Nothing()
    {
        EventDelivery delivery = SuccessfulDelivery();

        Should.NotThrow(() => delivery.ThrowIfAny(null));
        Should.NotThrow(() => delivery.ThrowIfAny([]));
    }

    [Fact]
    public void Given_Failures_And_Skipped_Entries_When_Using_The_Standard_Ending_Then_It_Throws_The_Standard_Exception()
    {
        var cause = new InvalidOperationException("failure");
        var failure = new EventHandlerFailure(
            typeof(TestHandler), typeof(TestEvent), cause);
        EventDelivery delivery = DeliveryReturning(failure, count: 2);

        EventPublishException exception = Should.Throw<EventPublishException>(
            () => delivery.ThrowIfAny([failure], skippedHandlerCount: 1));

        exception.EventType.ShouldBe(typeof(TestEvent));
        exception.Failures.ShouldHaveSingleItem().ShouldBeSameAs(failure);
        exception.InnerExceptions.ShouldHaveSingleItem().ShouldBeSameAs(cause);
        exception.SkippedHandlerCount.ShouldBe(1);
        exception.Message.ShouldContain("1 of 2 handlers");
        exception.Message.ShouldContain("1 handler was skipped");
    }

    [Fact]
    public void Given_Failures_And_Skipped_Entries_When_Creating_Cancellation_Then_It_Uses_The_Delivery_Event()
    {
        using var source = new CancellationTokenSource();
        var cause = new InvalidOperationException("failure");
        var failure = new EventHandlerFailure(
            typeof(TestHandler), typeof(TestEvent), cause);
        EventDelivery delivery = DeliveryReturning(failure, count: 2);

        EventPublishCanceledException exception = delivery.Canceled(
            [failure], skippedHandlerCount: 1, source.Token);

        exception.EventType.ShouldBe(typeof(TestEvent));
        exception.Failures.ShouldHaveSingleItem().ShouldBeSameAs(failure);
        exception.SkippedHandlerCount.ShouldBe(1);
        exception.CancellationToken.ShouldBe(source.Token);
    }

    [Fact]
    public void Given_A_Subscription_List_When_Changing_It_After_Over_Then_The_Delivery_Keeps_Its_Copy()
    {
        var subscriptions = new List<EventSubscription>
        {
            new(typeof(TestHandler), typeof(TestEvent)),
        };
        EventDelivery delivery = EventDelivery.Over(
            new TestEvent(),
            subscriptions,
            (_, _) => Task.FromResult<EventHandlerFailure?>(null));

        subscriptions.Clear();

        delivery.Count.ShouldBe(1);
        delivery[0].HandlerType.ShouldBe(typeof(TestHandler));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_A_Null_Subscription_Type_When_Constructing_Then_Throws_Argument_Null_Exception(
        bool handlerIsNull)
    {
        Action create = handlerIsNull
            ? () => _ = new EventSubscription(null!, typeof(TestEvent))
            : () => _ = new EventSubscription(typeof(TestHandler), null!);

        ArgumentNullException exception = Should.Throw<ArgumentNullException>(create);

        exception.ParamName.ShouldBe(handlerIsNull ? "handlerType" : "declaredEventType");
    }

    #region Helpers

    private static EventDelivery SuccessfulDelivery()
        => DeliveryReturning(null);

    private static EventDelivery DeliveryReturning(
        EventHandlerFailure? failure, int count = 1)
    {
        var subscriptions = new EventSubscription[count];
        for (int i = 0; i < subscriptions.Length; i++)
            subscriptions[i] = new EventSubscription(typeof(TestHandler), typeof(TestEvent));

        return EventDelivery.Over(
            new TestEvent(),
            subscriptions,
            (_, _) => Task.FromResult(failure));
    }

    private sealed record TestEvent : IEvent;

    private sealed class TestHandler;

    #endregion
}
