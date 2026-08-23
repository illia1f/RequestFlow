using RequestFlow;

namespace RequestFlow.Tests.Unit.Events;

public sealed class BuiltInEventPublishStrategyParityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Given_Mixed_Specificity_Entries_When_Publishing_Then_The_Strategy_And_Plan_Use_The_Frozen_Tier_Order(
        int strategyIndex)
    {
        EventModel model = TierModel();
        var strategyCalls = new List<Type>();
        var subscriptions = new EventSubscription[model.Handlers.Count];
        for (int i = 0; i < subscriptions.Length; i++)
        {
            EventHandlerModel handler = model.Handlers[i];
            subscriptions[i] = new EventSubscription(
                handler.HandlerType,
                handler.DeclaredEventType);
        }

        EventDelivery delivery = EventDelivery.Over(
            new TierEvent(),
            subscriptions,
            (index, _) =>
            {
                strategyCalls.Add(subscriptions[index].HandlerType);
                return Task.FromResult<EventHandlerFailure?>(null);
            });
        var planCalls = new List<Type>();
        var entries = new HandlerEntry[model.Handlers.Count];
        for (int i = 0; i < entries.Length; i++)
        {
            int index = i;
            entries[i] = (_, _, _) =>
            {
                planCalls.Add(model.Handlers[index].HandlerType);
                return Task.CompletedTask;
            };
        }
        EventPlan plan = PlanAt(strategyIndex, model, entries);

        await StrategyAt(strategyIndex).PublishAsync(delivery, default);
        await plan.ExecuteAsync(
            new TierEvent(),
            Substitute.For<IServiceProvider>(),
            default);

        Type[] expected = [
            typeof(ExactTierHandler),
            typeof(BaseTierHandler),
            typeof(InterfaceTierHandler),
            typeof(UniversalTierHandler),
        ];
        strategyCalls.ShouldBe(expected, ignoreOrder: false);
        planCalls.ShouldBe(expected, ignoreOrder: false);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Given_Ordered_Failures_When_Publishing_Then_The_Strategy_And_Plan_Outcomes_Match(
        int strategyIndex)
    {
        static Task Start(int index, CancellationToken _) => index switch
        {
            0 => Task.FromException(new InvalidOperationException("first")),
            2 => Task.FromException(new ArgumentException("last")),
            _ => Task.CompletedTask,
        };

        PublicationResult strategy = await RunStrategy(strategyIndex, 3, Start);
        PublicationResult plan = await RunPlan(strategyIndex, 3, Start);

        AssertEquivalent(strategy, plan);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Given_Cancellation_After_The_First_Failure_When_Publishing_Then_The_Strategy_And_Plan_Outcomes_Match(
        int strategyIndex)
    {
        using var strategySource = new CancellationTokenSource();
        using var planSource = new CancellationTokenSource();
        Task StrategyStart(int index, CancellationToken _)
        {
            if (index == 0)
            {
                strategySource.Cancel();
                return Task.FromException(new InvalidOperationException("failure"));
            }

            return Task.CompletedTask;
        }

        Task PlanStart(int index, CancellationToken _)
        {
            if (index == 0)
            {
                planSource.Cancel();
                return Task.FromException(new InvalidOperationException("failure"));
            }

            return Task.CompletedTask;
        }

        PublicationResult strategy = await RunStrategy(
            strategyIndex, 3, StrategyStart, strategySource.Token);
        PublicationResult plan = await RunPlan(
            strategyIndex, 3, PlanStart, planSource.Token);

        AssertEquivalent(strategy, plan);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Given_A_Pre_Canceled_Empty_Publish_When_Publishing_Then_The_Strategy_And_Plan_Outcomes_Match(
        int strategyIndex)
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        PublicationResult strategy = await RunStrategy(
            strategyIndex, 0, (_, _) => Task.CompletedTask, source.Token);
        PublicationResult plan = await RunPlan(
            strategyIndex, 0, (_, _) => Task.CompletedTask, source.Token);

        AssertEquivalent(strategy, plan);
    }

    #region Helpers

    private static async Task<PublicationResult> RunStrategy(
        int strategyIndex,
        int count,
        Func<int, CancellationToken, Task> start,
        CancellationToken cancellationToken = default)
    {
        var calls = new List<int>();
        var tokens = new List<CancellationToken>();
        EventDelivery delivery = EventDelivery.Over(
            new TestEvent(),
            Subscriptions(count),
            async (index, token) =>
            {
                calls.Add(index);
                tokens.Add(token);
                Task task = start(index, token);
                try
                {
                    await task.ConfigureAwait(false);
                    return null;
                }
                catch (Exception exception)
                {
                    Exception cause = task.IsFaulted
                        ? task.Exception!.InnerExceptions.Count == 1
                            ? task.Exception.InnerExceptions[0]
                            : task.Exception
                        : exception;
                    return Failure(index, cause);
                }
            },
            cancellationToken: cancellationToken);

        Task publish = StrategyAt(strategyIndex).PublishAsync(delivery, cancellationToken);
        Exception? exception = await ExceptionFrom(publish);
        return new PublicationResult(calls, tokens, publish.IsCanceled, exception);
    }

    private static async Task<PublicationResult> RunPlan(
        int strategyIndex,
        int count,
        Func<int, CancellationToken, Task> start,
        CancellationToken cancellationToken = default)
    {
        var calls = new List<int>();
        var tokens = new List<CancellationToken>();
        EventHandlerModel[] handlers = Handlers(count);
        var model = new EventModel(typeof(TestEvent), handlers);
        var entries = new HandlerEntry[count];
        for (int i = 0; i < entries.Length; i++)
        {
            int index = i;
            entries[i] = (_, _, token) =>
            {
                calls.Add(index);
                tokens.Add(token);
                return start(index, token);
            };
        }

        EventPlan plan = PlanAt(strategyIndex, model, entries);
        Task publish = plan.ExecuteAsync(
            new TestEvent(),
            Substitute.For<IServiceProvider>(),
            cancellationToken);
        Exception? exception = await ExceptionFrom(publish);
        return new PublicationResult(calls, tokens, publish.IsCanceled, exception);
    }

    private static void AssertEquivalent(PublicationResult strategy, PublicationResult plan)
    {
        strategy.Calls.ShouldBe(plan.Calls, ignoreOrder: false);
        strategy.Tokens.Count.ShouldBe(plan.Tokens.Count);
        for (int i = 0; i < strategy.Tokens.Count; i++)
        {
            strategy.Tokens[i].CanBeCanceled.ShouldBe(plan.Tokens[i].CanBeCanceled);
            strategy.Tokens[i].IsCancellationRequested
                .ShouldBe(plan.Tokens[i].IsCancellationRequested);
        }
        strategy.IsCanceled.ShouldBe(plan.IsCanceled);

        if (strategy.Exception is EventPublishException strategyFailure)
        {
            EventPublishException planFailure = plan.Exception
                .ShouldBeOfType<EventPublishException>();
            strategyFailure.Message.ShouldBe(planFailure.Message);
            strategyFailure.SkippedHandlerCount.ShouldBe(planFailure.SkippedHandlerCount);
            strategyFailure.Failures.Count.ShouldBe(planFailure.Failures.Count);
            strategyFailure.InnerExceptions.Count.ShouldBe(planFailure.InnerExceptions.Count);
            for (int i = 0; i < strategyFailure.Failures.Count; i++)
            {
                EventHandlerFailure strategyEntry = strategyFailure.Failures[i];
                EventHandlerFailure planEntry = planFailure.Failures[i];
                strategyEntry.HandlerType.ShouldBe(planEntry.HandlerType);
                strategyEntry.DeclaredEventType.ShouldBe(planEntry.DeclaredEventType);
                strategyEntry.Exception.GetType().ShouldBe(planEntry.Exception.GetType());
                strategyEntry.Exception.Message.ShouldBe(planEntry.Exception.Message);
                strategyFailure.InnerExceptions[i].ShouldBeSameAs(strategyEntry.Exception);
                planFailure.InnerExceptions[i].ShouldBeSameAs(planEntry.Exception);
            }

            return;
        }

        if (strategy.Exception is EventPublishCanceledException strategyCanceled)
        {
            EventPublishCanceledException planCanceled = plan.Exception
                .ShouldBeOfType<EventPublishCanceledException>();
            strategyCanceled.Message.ShouldBe(planCanceled.Message);
            strategyCanceled.SkippedHandlerCount.ShouldBe(planCanceled.SkippedHandlerCount);
            strategyCanceled.Failures.Count.ShouldBe(planCanceled.Failures.Count);
            for (int i = 0; i < strategyCanceled.Failures.Count; i++)
            {
                EventHandlerFailure strategyEntry = strategyCanceled.Failures[i];
                EventHandlerFailure planEntry = planCanceled.Failures[i];
                strategyEntry.HandlerType.ShouldBe(planEntry.HandlerType);
                strategyEntry.DeclaredEventType.ShouldBe(planEntry.DeclaredEventType);
                strategyEntry.Exception.GetType().ShouldBe(planEntry.Exception.GetType());
                strategyEntry.Exception.Message.ShouldBe(planEntry.Exception.Message);
            }

            if (strategyCanceled.Failures.Count > 0)
            {
                AggregateException strategyInner = strategyCanceled.InnerException
                    .ShouldBeOfType<AggregateException>();
                AggregateException planInner = planCanceled.InnerException
                    .ShouldBeOfType<AggregateException>();
                for (int i = 0; i < strategyCanceled.Failures.Count; i++)
                {
                    strategyInner.InnerExceptions[i]
                        .ShouldBeSameAs(strategyCanceled.Failures[i].Exception);
                    planInner.InnerExceptions[i]
                        .ShouldBeSameAs(planCanceled.Failures[i].Exception);
                }
            }

            return;
        }

        strategy.Exception.ShouldBeNull();
        plan.Exception.ShouldBeNull();
    }

    private static EventHandlerModel[] Handlers(int count)
    {
        var handlers = new EventHandlerModel[count];
        for (int i = 0; i < handlers.Length; i++)
        {
            handlers[i] = new EventHandlerModel(
                HandlerType(i),
                typeof(TestEvent),
                RequestFlowLifetime.Transient);
        }

        return handlers;
    }

    private static EventModel TierModel()
    {
        EventClosureResult closure = EventClosure.Build(
            [typeof(TierEvent)],
            [
                new EventSubscriptionInput(
                    typeof(UniversalTierHandler),
                    typeof(IEvent),
                    RequestFlowLifetime.Transient),
                new EventSubscriptionInput(
                    typeof(InterfaceTierHandler),
                    typeof(ITierEvent),
                    RequestFlowLifetime.Transient),
                new EventSubscriptionInput(
                    typeof(ExactTierHandler),
                    typeof(TierEvent),
                    RequestFlowLifetime.Transient),
                new EventSubscriptionInput(
                    typeof(BaseTierHandler),
                    typeof(TierBaseEvent),
                    RequestFlowLifetime.Transient),
            ]);

        return closure.Events.Single(@event => @event.EventType == typeof(TierEvent));
    }

    private static EventPlan PlanAt(
        int strategyIndex,
        EventModel model,
        HandlerEntry[] entries)
        => strategyIndex switch
        {
            0 => new SequentialEventPlan(model, entries),
            1 => new ParallelEventPlan(model, entries),
            2 => new SequentialEventPlan(model, entries, stopOnFirstFailure: true),
            _ => throw new ArgumentOutOfRangeException(nameof(strategyIndex)),
        };

    private static EventSubscription[] Subscriptions(int count)
    {
        var subscriptions = new EventSubscription[count];
        for (int i = 0; i < subscriptions.Length; i++)
            subscriptions[i] = new EventSubscription(HandlerType(i), typeof(TestEvent));

        return subscriptions;
    }

    private static Type HandlerType(int index) => index switch
    {
        0 => typeof(AlphaHandler),
        1 => typeof(MiddleHandler),
        2 => typeof(ZuluHandler),
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    private static EventHandlerFailure Failure(int index, Exception exception)
        => new(HandlerType(index), typeof(TestEvent), exception);

    private static IEventPublishStrategy StrategyAt(int index) => index switch
    {
        0 => new SequentialPublishStrategy(),
        1 => new ParallelPublishStrategy(),
        2 => new FailFastPublishStrategy(),
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

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

    private sealed record TestEvent : IEvent;

    private interface ITierEvent : IEvent;

    private record TierBaseEvent : IEvent;

    private sealed record TierEvent : TierBaseEvent, ITierEvent;

    private sealed class AlphaHandler;

    private sealed class MiddleHandler;

    private sealed class ZuluHandler;

    private sealed class ExactTierHandler;

    private sealed class BaseTierHandler;

    private sealed class InterfaceTierHandler;

    private sealed class UniversalTierHandler;

    private sealed class PublicationResult(
        List<int> calls,
        List<CancellationToken> tokens,
        bool isCanceled,
        Exception? exception)
    {
        public List<int> Calls { get; } = calls;

        public List<CancellationToken> Tokens { get; } = tokens;

        public bool IsCanceled { get; } = isCanceled;

        public Exception? Exception { get; } = exception;
    }

    #endregion
}
