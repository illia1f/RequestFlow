using System.Collections;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class EventExceptionTests
{
    [Fact]
    public void Given_A_General_Event_Handler_When_Assigning_It_To_A_Specific_Handler_Then_The_Assignment_Is_Valid()
    {
        IEventHandler<IEvent> general = Substitute.For<IEventHandler<IEvent>>();

        IEventHandler<SpecificEvent> specific = general;

        specific.ShouldBeSameAs(general);
    }

    [Fact]
    public void Given_Event_Handler_Failure_Arguments_When_Creating_A_Failure_Then_They_Are_Exposed()
    {
        var cause = new InvalidOperationException("boom");

        var failure = new EventHandlerFailure(typeof(SpecificHandler), typeof(SpecificEvent), cause);

        failure.HandlerType.ShouldBe(typeof(SpecificHandler));
        failure.DeclaredEventType.ShouldBe(typeof(SpecificEvent));
        failure.Exception.ShouldBeSameAs(cause);
    }

    [Theory]
    [InlineData("handlerType")]
    [InlineData("declaredEventType")]
    [InlineData("exception")]
    public void Given_A_Null_Event_Handler_Failure_Argument_When_Creating_A_Failure_Then_Throws_Argument_Null_Exception(
        string argumentName)
    {
        ArgumentNullException exception = argumentName switch
        {
            "handlerType" => Should.Throw<ArgumentNullException>(
                () => new EventHandlerFailure(null!, typeof(SpecificEvent), new InvalidOperationException())),
            "declaredEventType" => Should.Throw<ArgumentNullException>(
                () => new EventHandlerFailure(typeof(SpecificHandler), null!, new InvalidOperationException())),
            _ => Should.Throw<ArgumentNullException>(
                () => new EventHandlerFailure(typeof(SpecificHandler), typeof(SpecificEvent), null!))
        };

        exception.ParamName.ShouldBe(argumentName);
    }

    [Fact]
    public void Given_Failures_When_Creating_A_Publish_Exception_Then_They_And_Their_Exceptions_Keep_Their_Order()
    {
        EventHandlerFailure first = Failure("first");
        EventHandlerFailure second = Failure("second");

        var exception = new EventPublishException(typeof(SpecificEvent), [first, second]);

        exception.EventType.ShouldBe(typeof(SpecificEvent));
        exception.Failures.ShouldBe([first, second], ignoreOrder: false);
        exception.InnerExceptions.ShouldBe([first.Exception, second.Exception], ignoreOrder: false);
        exception.SkippedHandlerCount.ShouldBe(0);
    }

    [Fact]
    public void Given_A_Mutable_Failure_List_When_Creating_A_Publish_Exception_Then_The_Exception_Keeps_A_Defensive_Copy()
    {
        EventHandlerFailure original = Failure("original");
        EventHandlerFailure replacement = Failure("replacement");
        var failures = new List<EventHandlerFailure> { original };

        var exception = new EventPublishException(typeof(SpecificEvent), failures);
        failures[0] = replacement;

        exception.Failures.ShouldHaveSingleItem().ShouldBeSameAs(original);
        exception.InnerExceptions.ShouldHaveSingleItem().ShouldBeSameAs(original.Exception);
    }

    [Fact]
    public void Given_A_Stateful_Failure_List_When_Creating_A_Publish_Exception_Then_All_Failure_Views_Use_One_Snapshot()
    {
        var first = new EventHandlerFailure(
            typeof(FirstHandler), typeof(SpecificEvent), new InvalidOperationException("first"));
        var subsequent = new EventHandlerFailure(
            typeof(SubsequentHandler), typeof(SubsequentEvent), new InvalidOperationException("subsequent"));
        var failures = new StatefulFailureList(first, subsequent);

        var exception = new EventPublishException(typeof(SpecificEvent), failures);

        exception.Failures.ShouldHaveSingleItem().ShouldBeSameAs(first);
        exception.InnerExceptions.ShouldHaveSingleItem().ShouldBeSameAs(first.Exception);
        exception.Message.ShouldStartWith(
            $"Publishing '{typeof(SpecificEvent).FullName}' failed in 1 handlers: " +
            $"'{typeof(FirstHandler).FullName}' declared for '{typeof(SpecificEvent).FullName}'.");
        exception.Message.ShouldNotContain(typeof(SubsequentHandler).FullName!);
        failures.IndexReadCount.ShouldBe(1);
    }

    [Fact]
    public void Given_No_Handler_Total_When_Creating_A_Publish_Exception_Then_The_Message_Counts_Only_The_Failures()
    {
        var exception = new EventPublishException(
            typeof(SpecificEvent), [Failure("first"), Failure("second")]);

        exception.Message.ShouldStartWith(
            $"Publishing '{typeof(SpecificEvent).FullName}' failed in 2 handlers: ");
        exception.Message.ShouldNotContain(" of ");
    }

    [Theory]
    [InlineData("eventType")]
    [InlineData("failures")]
    public void Given_A_Null_Publish_Exception_Argument_When_Creating_The_Exception_Then_Throws_Argument_Null_Exception(
        string argumentName)
    {
        ArgumentNullException exception = argumentName switch
        {
            "eventType" => Should.Throw<ArgumentNullException>(
                () => new EventPublishException(null!, [Failure("failure")])),
            _ => Should.Throw<ArgumentNullException>(
                () => new EventPublishException(typeof(SpecificEvent), null!))
        };

        exception.ParamName.ShouldBe(argumentName);
    }

    [Fact]
    public void Given_An_Empty_Failure_List_When_Creating_A_Publish_Exception_Then_Throws_Argument_Exception()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => new EventPublishException(typeof(SpecificEvent), []));

        exception.ParamName.ShouldBe("failures");
    }

    [Fact]
    public void Given_A_Null_Failure_In_A_Publish_Exception_List_When_Creating_The_Exception_Then_Throws_Argument_Exception()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => new EventPublishException(typeof(SpecificEvent), [null!]));

        exception.ParamName.ShouldBe("failures");
    }

    [Fact]
    public void Given_Failures_And_A_Publishing_Token_When_Creating_A_Canceled_Exception_Then_They_Are_Exposed()
    {
        EventHandlerFailure failure = Failure("failure");
        using var source = new CancellationTokenSource();

        var exception = new EventPublishCanceledException(
            typeof(SpecificEvent), source.Token, [failure], skippedHandlerCount: 2);

        exception.EventType.ShouldBe(typeof(SpecificEvent));
        exception.CancellationToken.ShouldBe(source.Token);
        exception.SkippedHandlerCount.ShouldBe(2);
        exception.Failures.ShouldHaveSingleItem().ShouldBeSameAs(failure);
        AggregateException aggregate = exception.InnerException.ShouldBeOfType<AggregateException>();
        aggregate.InnerExceptions.ShouldHaveSingleItem().ShouldBeSameAs(failure.Exception);
    }

    [Fact]
    public void Given_A_Stateful_Failure_List_When_Creating_A_Canceled_Exception_Then_All_Failure_Views_Use_One_Snapshot()
    {
        var first = new EventHandlerFailure(
            typeof(FirstHandler), typeof(SpecificEvent), new InvalidOperationException("first"));
        var subsequent = new EventHandlerFailure(
            typeof(SubsequentHandler), typeof(SubsequentEvent), new InvalidOperationException("subsequent"));
        var failures = new StatefulFailureList(first, subsequent);

        var exception = new EventPublishCanceledException(
            typeof(SpecificEvent), default, failures, skippedHandlerCount: 0);

        exception.Failures.ShouldHaveSingleItem().ShouldBeSameAs(first);
        AggregateException aggregate = exception.InnerException.ShouldBeOfType<AggregateException>();
        aggregate.InnerExceptions.ShouldHaveSingleItem().ShouldBeSameAs(first.Exception);
        exception.Message.ShouldBe(
            $"Publishing '{typeof(SpecificEvent).FullName}' was canceled after 1 handler failures; " +
            $"0 handlers were skipped: '{typeof(FirstHandler).FullName}' declared for " +
            $"'{typeof(SpecificEvent).FullName}'.");
        failures.IndexReadCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("eventType")]
    [InlineData("failures")]
    public void Given_A_Null_Canceled_Exception_Argument_When_Creating_The_Exception_Then_Throws_Argument_Null_Exception(
        string argumentName)
    {
        ArgumentNullException exception = argumentName switch
        {
            "eventType" => Should.Throw<ArgumentNullException>(
                () => new EventPublishCanceledException(null!, default, [Failure("failure")], 0)),
            _ => Should.Throw<ArgumentNullException>(
                () => new EventPublishCanceledException(typeof(SpecificEvent), default, null!, 0))
        };

        exception.ParamName.ShouldBe(argumentName);
    }

    [Fact]
    public void Given_A_Null_Failure_In_A_Canceled_Exception_List_When_Creating_The_Exception_Then_Throws_Argument_Exception()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => new EventPublishCanceledException(typeof(SpecificEvent), default, [null!], 0));

        exception.ParamName.ShouldBe("failures");
    }

    [Fact]
    public void Given_No_Failures_When_Creating_A_Canceled_Exception_Then_The_Message_Ends_After_The_Skipped_Count()
    {
        var exception = new EventPublishCanceledException(
            typeof(SpecificEvent), default, [], skippedHandlerCount: 3);

        exception.SkippedHandlerCount.ShouldBe(3);
        exception.Failures.ShouldBeEmpty();
        exception.InnerException.ShouldBeNull();
        exception.Message.ShouldBe(
            $"Publishing '{typeof(SpecificEvent).FullName}' was canceled before any handler failed; " +
            "3 handlers were skipped.");
    }

    [Fact]
    public void Given_A_Negative_Skipped_Count_When_Creating_A_Canceled_Exception_Then_Throws_Argument_Out_Of_Range_Exception()
    {
        ArgumentOutOfRangeException exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new EventPublishCanceledException(typeof(SpecificEvent), default, [], -1));

        exception.ParamName.ShouldBe("skippedHandlerCount");
    }

    [Fact]
    public void Given_An_Unknown_Event_When_Creating_A_Not_Registered_Exception_Then_The_Message_Names_The_Missing_Entry()
    {
        var exception = new EventNotRegisteredException(typeof(SpecificEvent));

        exception.Message.ShouldStartWith(
            $"Event type '{typeof(SpecificEvent).FullName}' has no entry in the event map");
        exception.Message.ShouldContain("RegisterHandlersFromAssembly");
    }

    [Fact]
    public void Given_An_Event_Type_When_Creating_A_Not_Registered_Exception_Then_It_Is_Exposed()
    {
        var exception = new EventNotRegisteredException(typeof(SpecificEvent));

        exception.EventType.ShouldBe(typeof(SpecificEvent));
    }

    [Fact]
    public void Given_A_Null_Event_Type_When_Creating_A_Not_Registered_Exception_Then_Throws_Argument_Null_Exception()
    {
        ArgumentNullException exception = Should.Throw<ArgumentNullException>(
            () => new EventNotRegisteredException(null!));

        exception.ParamName.ShouldBe("eventType");
    }

    [Fact]
    public void Given_The_Event_And_Handler_Types_When_Creating_A_Null_Task_Exception_Then_They_Are_Exposed()
    {
        var exception = new EventHandlerNullTaskException(typeof(SpecificEvent), typeof(SpecificHandler));

        exception.ShouldBeAssignableTo<NullTaskException>();
        exception.EventType.ShouldBe(typeof(SpecificEvent));
        exception.HandlerType.ShouldBe(typeof(SpecificHandler));
    }

    [Theory]
    [InlineData("eventType")]
    [InlineData("handlerType")]
    public void Given_A_Null_Null_Task_Exception_Argument_When_Creating_The_Exception_Then_Throws_Argument_Null_Exception(
        string argumentName)
    {
        ArgumentNullException exception = argumentName switch
        {
            "eventType" => Should.Throw<ArgumentNullException>(
                () => new EventHandlerNullTaskException(null!, typeof(SpecificHandler))),
            _ => Should.Throw<ArgumentNullException>(
                () => new EventHandlerNullTaskException(typeof(SpecificEvent), null!))
        };

        exception.ParamName.ShouldBe(argumentName);
    }

    [Fact]
    public void Given_A_Strategy_Type_When_Creating_A_Strategy_Null_Task_Exception_Then_It_Is_Exposed()
    {
        var exception = new EventStrategyNullTaskException(typeof(TestStrategy));

        exception.ShouldBeAssignableTo<NullTaskException>();
        exception.StrategyType.ShouldBe(typeof(TestStrategy));
        exception.Message.ShouldContain(typeof(TestStrategy).FullName!);
        exception.Message.ShouldContain("PublishAsync");
    }

    [Fact]
    public void Given_A_Null_Strategy_Type_When_Creating_A_Strategy_Null_Task_Exception_Then_Throws_Argument_Null_Exception()
    {
        ArgumentNullException exception = Should.Throw<ArgumentNullException>(
            () => new EventStrategyNullTaskException(null!));

        exception.ParamName.ShouldBe("strategyType");
    }

    #region Helpers

    private static EventHandlerFailure Failure(string message)
        => new(typeof(SpecificHandler), typeof(SpecificEvent), new InvalidOperationException(message));

    private sealed class StatefulFailureList : IReadOnlyList<EventHandlerFailure>
    {
        private readonly EventHandlerFailure _first;
        private readonly EventHandlerFailure _subsequent;

        public StatefulFailureList(EventHandlerFailure first, EventHandlerFailure subsequent)
        {
            _first = first;
            _subsequent = subsequent;
        }

        public EventHandlerFailure this[int index]
        {
            get
            {
                if (index != 0)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return IndexReadCount++ == 0 ? _first : _subsequent;
            }
        }

        public int Count => 1;

        public int IndexReadCount { get; private set; }

        public IEnumerator<EventHandlerFailure> GetEnumerator()
        {
            yield return this[0];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed record SpecificEvent : IEvent;

    private sealed record SubsequentEvent : IEvent;

    private sealed class SpecificHandler;

    private sealed class FirstHandler;

    private sealed class SubsequentHandler;

    private sealed class TestStrategy;

    #endregion
}
