using RequestFlow;

namespace RequestFlow.Tests.Unit.Events;

public sealed class BuiltInEventPublishStrategyTests
{
    [Fact]
    public async Task Given_A_Failing_Entry_When_Publishing_Sequentially_Then_Later_Entries_Run_And_The_Failure_Is_Thrown()
    {
        var calls = new List<int>();
        var cause = new InvalidOperationException("failure");
        EventDelivery delivery = Delivery(2, (index, _) =>
        {
            calls.Add(index);
            EventHandlerFailure? failure = index == 0
                ? Failure(cause)
                : null;
            return Task.FromResult(failure);
        });
        var sut = new SequentialPublishStrategy();

        EventPublishException exception = await Should.ThrowAsync<EventPublishException>(
            () => sut.PublishAsync(delivery, default));

        calls.ShouldBe([0, 1]);
        exception.Failures.ShouldHaveSingleItem().Exception.ShouldBeSameAs(cause);
        exception.SkippedHandlerCount.ShouldBe(0);
    }

    [Fact]
    public async Task Given_Incomplete_Entries_When_Publishing_In_Parallel_Then_All_Start_Before_Any_Is_Awaited()
    {
        var calls = new List<int>();
        var firstGate = new TaskCompletionSource<EventHandlerFailure?>();
        EventDelivery delivery = Delivery(2, (index, _) =>
        {
            calls.Add(index);
            return index == 0
                ? firstGate.Task
                : Task.FromResult<EventHandlerFailure?>(null);
        });
        var sut = new ParallelPublishStrategy();

        Task publish = sut.PublishAsync(delivery, default);

        calls.ShouldBe([0, 1]);
        publish.IsCompleted.ShouldBeFalse();
        firstGate.SetResult(null);
        await publish;
    }

    [Fact]
    public async Task Given_A_Failing_Entry_When_Publishing_Fail_Fast_Then_Later_Entries_Never_Start()
    {
        var calls = new List<int>();
        var cause = new InvalidOperationException("failure");
        EventDelivery delivery = Delivery(3, (index, _) =>
        {
            calls.Add(index);
            EventHandlerFailure? failure = index == 1
                ? Failure(cause)
                : null;
            return Task.FromResult(failure);
        });
        var sut = new FailFastPublishStrategy();

        EventPublishException exception = await Should.ThrowAsync<EventPublishException>(
            () => sut.PublishAsync(delivery, default));

        calls.ShouldBe([0, 1]);
        exception.Failures.ShouldHaveSingleItem().Exception.ShouldBeSameAs(cause);
        exception.SkippedHandlerCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Given_A_Pre_Canceled_Token_When_Publishing_An_Empty_Delivery_Then_Returns_Cancellation(
        int strategyIndex)
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        IEventPublishStrategy sut = StrategyAt(strategyIndex);
        EventDelivery delivery = Delivery(0, (_, _) =>
            Task.FromResult<EventHandlerFailure?>(null));

        Task publish = sut.PublishAsync(delivery, source.Token);
        EventPublishCanceledException exception = (await ExceptionFrom(publish))
            .ShouldBeOfType<EventPublishCanceledException>();

        exception.CancellationToken.ShouldBe(source.Token);
        exception.SkippedHandlerCount.ShouldBe(0);
        publish.IsCanceled.ShouldBeTrue();
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

    private static EventDelivery Delivery(
        int count,
        Func<int, CancellationToken, Task<EventHandlerFailure?>> start)
    {
        var subscriptions = new EventSubscription[count];
        for (int i = 0; i < subscriptions.Length; i++)
            subscriptions[i] = new EventSubscription(typeof(TestHandler), typeof(TestEvent));

        return EventDelivery.Over(new TestEvent(), subscriptions, start);
    }

    private static EventHandlerFailure Failure(Exception exception)
        => new(typeof(TestHandler), typeof(TestEvent), exception);

    private static IEventPublishStrategy StrategyAt(int index)
        => index switch
        {
            0 => new SequentialPublishStrategy(),
            1 => new ParallelPublishStrategy(),
            2 => new FailFastPublishStrategy(),
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

    private sealed record TestEvent : IEvent;

    private sealed class TestHandler;

    #endregion
}
