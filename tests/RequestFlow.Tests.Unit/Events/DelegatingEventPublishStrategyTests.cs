using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Events;

// The built-ins are public so a custom strategy can hand its delivery to one. These run that path
// through the container, over a plan-built delivery, against the specialized plan for the same
// built-in.
public sealed class DelegatingEventPublishStrategyTests
{
    [Fact]
    public async Task Given_A_Strategy_Delegating_To_The_Sequential_Built_In_When_A_Handler_Fails_Then_The_Outcome_Matches_The_Plan()
    {
        PublishOutcome delegated = await PublishWith<DelegatingSequentialStrategy>();
        PublishOutcome specialized = await PublishWith<SequentialPublishStrategy>();

        AssertEquivalent(delegated, specialized);
        delegated.Failures.ShouldHaveSingleItem().HandlerType.ShouldBe(typeof(AlphaDelegatedHandler));
        delegated.Calls.ShouldBe(["alpha", "zulu"], ignoreOrder: true);
        delegated.SkippedHandlerCount.ShouldBe(0);
    }

    [Fact]
    public async Task Given_A_Strategy_Delegating_To_The_Parallel_Built_In_When_A_Handler_Fails_Then_The_Outcome_Matches_The_Plan()
    {
        PublishOutcome delegated = await PublishWith<DelegatingParallelStrategy>();
        PublishOutcome specialized = await PublishWith<ParallelPublishStrategy>();

        AssertEquivalent(delegated, specialized);
        delegated.Failures.ShouldHaveSingleItem().HandlerType.ShouldBe(typeof(AlphaDelegatedHandler));
        delegated.Calls.ShouldBe(["alpha", "zulu"], ignoreOrder: true);
        delegated.SkippedHandlerCount.ShouldBe(0);
    }

    [Fact]
    public async Task Given_A_Strategy_Delegating_To_The_Fail_Fast_Built_In_When_A_Handler_Fails_Then_The_Outcome_Matches_The_Plan()
    {
        PublishOutcome delegated = await PublishWith<DelegatingFailFastStrategy>();
        PublishOutcome specialized = await PublishWith<FailFastPublishStrategy>();

        AssertEquivalent(delegated, specialized);
        delegated.Failures.ShouldHaveSingleItem().HandlerType.ShouldBe(typeof(AlphaDelegatedHandler));
        delegated.Calls.ShouldBe(["alpha"]);
        delegated.SkippedHandlerCount.ShouldBeGreaterThan(0);
    }

    #region Helpers

    private static async Task<PublishOutcome> PublishWith<TStrategy>()
        where TStrategy : class, IEventPublishStrategy
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options => options
            .RegisterHandlersFromAssemblyContaining<DelegatingEventPublishStrategyTests>()
            .PublishEventsWith<DelegatedEvent, TStrategy>());
        using ServiceProvider provider = services.BuildServiceProvider();
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();
        var @event = new DelegatedEvent(new InvalidOperationException("delegated failure"));

        EventPublishException exception = await Should.ThrowAsync<EventPublishException>(
            () => publisher.PublishAsync(@event));

        return new PublishOutcome(
            exception.EventType,
            exception.SkippedHandlerCount,
            exception.Failures,
            @event.Calls);
    }

    private static void AssertEquivalent(PublishOutcome delegated, PublishOutcome specialized)
    {
        delegated.EventType.ShouldBe(specialized.EventType);
        delegated.SkippedHandlerCount.ShouldBe(specialized.SkippedHandlerCount);
        delegated.Calls.ShouldBe(specialized.Calls, ignoreOrder: true);
        delegated.Failures.Count.ShouldBe(specialized.Failures.Count);
        for (int i = 0; i < delegated.Failures.Count; i++)
        {
            EventHandlerFailure left = delegated.Failures[i];
            EventHandlerFailure right = specialized.Failures[i];
            left.HandlerType.ShouldBe(right.HandlerType);
            left.DeclaredEventType.ShouldBe(right.DeclaredEventType);
            left.Exception.Message.ShouldBe(right.Exception.Message);
        }
    }

    private sealed record PublishOutcome(
        Type EventType,
        int SkippedHandlerCount,
        IReadOnlyList<EventHandlerFailure> Failures,
        IReadOnlyList<string> Calls);

    public sealed record DelegatedEvent(Exception Failure) : IEvent
    {
        public List<string> Calls { get; } = [];
    }

    public sealed class AlphaDelegatedHandler : IEventHandler<DelegatedEvent>
    {
        public Task HandleAsync(DelegatedEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("alpha");
            throw @event.Failure;
        }
    }

    public sealed class ZuluDelegatedHandler : IEventHandler<DelegatedEvent>
    {
        public Task HandleAsync(DelegatedEvent @event, CancellationToken cancellationToken)
        {
            @event.Calls.Add("zulu");
            return Task.CompletedTask;
        }
    }

    public sealed class DelegatingSequentialStrategy : IEventPublishStrategy
    {
        public Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken)
            => new SequentialPublishStrategy().PublishAsync(delivery, cancellationToken);
    }

    public sealed class DelegatingParallelStrategy : IEventPublishStrategy
    {
        public Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken)
            => new ParallelPublishStrategy().PublishAsync(delivery, cancellationToken);
    }

    public sealed class DelegatingFailFastStrategy : IEventPublishStrategy
    {
        public Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken)
            => new FailFastPublishStrategy().PublishAsync(delivery, cancellationToken);
    }

    #endregion
}
