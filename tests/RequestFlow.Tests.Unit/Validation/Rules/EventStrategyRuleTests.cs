using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class EventStrategyRuleTests
{
    [Fact]
    public void Given_An_Interface_Strategy_When_Validating_Then_Reports_RF0013()
    {
        RequestFlowValidationProblem problem = Validate(
            [],
            [Strategy(null, typeof(IInvalidStrategy))]).ShouldHaveSingleItem();

        problem.Code.ShouldBe("RF0013");
        problem.Subject.ShouldBe(typeof(IInvalidStrategy));
    }

    [Fact]
    public void Given_An_Abstract_Strategy_When_Validating_Then_Reports_RF0014()
    {
        RequestFlowValidationProblem problem = Validate(
            [],
            [Strategy(null, typeof(AbstractStrategy))]).ShouldHaveSingleItem();

        problem.Code.ShouldBe("RF0014");
        problem.Subject.ShouldBe(typeof(AbstractStrategy));
    }

    [Fact]
    public void Given_Two_Different_Global_Strategies_When_Validating_Then_Reports_RF0119()
    {
        RequestFlowValidationProblem problem = Validate(
            [typeof(TestEvent)],
            [Strategy(null, typeof(StrategyA)), Strategy(null, typeof(StrategyB))])
            .ShouldHaveSingleItem();

        problem.Code.ShouldBe("RF0119");
        problem.Subject.ShouldBeNull();
        problem.Message.ShouldContain("global");
        problem.Message.ShouldContain(typeof(StrategyA).FullName!);
        problem.Message.ShouldContain(typeof(StrategyB).FullName!);
    }

    [Fact]
    public void Given_One_Target_With_Two_Strategies_When_Validating_Then_Reports_RF0119_Only()
    {
        RequestFlowValidationProblem problem = Validate(
            [typeof(TestEvent)],
            [
                Strategy(typeof(TestEvent), typeof(StrategyA)),
                Strategy(typeof(TestEvent), typeof(StrategyB)),
            ]).ShouldHaveSingleItem();

        problem.Code.ShouldBe("RF0119");
        problem.Subject.ShouldBe(typeof(TestEvent));
    }

    [Fact]
    public void Given_Unrelated_Applicable_Interfaces_When_Validating_Then_Reports_RF0120()
    {
        RequestFlowValidationProblem problem = Validate(
            [typeof(MultiInterfaceEvent)],
            [
                Strategy(typeof(IFirstEvent), typeof(StrategyA)),
                Strategy(typeof(ISecondEvent), typeof(StrategyB)),
            ],
            unusedStrategiesDisallowed: true).ShouldHaveSingleItem();

        problem.Code.ShouldBe("RF0120");
        problem.Subject.ShouldBe(typeof(MultiInterfaceEvent));
        problem.Message.ShouldContain(typeof(IFirstEvent).FullName!);
        problem.Message.ShouldContain(typeof(ISecondEvent).FullName!);
    }

    [Fact]
    public void Given_One_Strategy_Type_With_Two_Lifetimes_When_Validating_Then_Reports_RF0121()
    {
        RequestFlowValidationProblem problem = Validate(
            [typeof(TestEvent)],
            [
                Strategy(null, typeof(StrategyA), RequestFlowLifetime.Singleton),
                Strategy(typeof(TestEvent), typeof(StrategyA), RequestFlowLifetime.Scoped),
            ]).ShouldHaveSingleItem();

        problem.Code.ShouldBe("RF0121");
        problem.Subject.ShouldBe(typeof(StrategyA));
        problem.Message.ShouldContain("Singleton");
        problem.Message.ShouldContain("Scoped");
    }

    [Fact]
    public void Given_A_Declaration_Applicable_To_No_Event_When_Unused_Declarations_Are_Disallowed_Then_Reports_RF0122()
    {
        RequestFlowValidationProblem problem = Validate(
            [typeof(TestEvent)],
            [Strategy(typeof(IUnreachedEvent), typeof(StrategyA))],
            unusedStrategiesDisallowed: true).ShouldHaveSingleItem();

        problem.Code.ShouldBe("RF0122");
        problem.Subject.ShouldBe(typeof(IUnreachedEvent));
    }

    [Theory]
    [InlineData(typeof(IFirstEvent), typeof(MultiInterfaceEvent), false)]
    [InlineData(typeof(IFirstEvent), typeof(MultiInterfaceEvent), true)]
    [InlineData(typeof(IEvent), typeof(IFirstEvent), false)]
    [InlineData(typeof(IEvent), typeof(IFirstEvent), true)]
    [InlineData(typeof(IFirstEvent), typeof(IDerivedEvent), false)]
    [InlineData(typeof(IFirstEvent), typeof(IDerivedEvent), true)]
    public void Given_A_Shadowed_Family_Declaration_When_Unused_Declarations_Are_Disallowed_Then_Reports_Nothing(
        Type shadowedTarget,
        Type winningTarget,
        bool winnerFirst)
    {
        EventStrategyInput[] strategies =
        [
            Strategy(shadowedTarget, typeof(StrategyA)),
            Strategy(winningTarget, typeof(StrategyB)),
        ];
        if (winnerFirst)
            Array.Reverse(strategies);

        IReadOnlyList<RequestFlowValidationProblem> problems = Validate(
            [typeof(MultiInterfaceEvent)],
            strategies,
            unusedStrategiesDisallowed: true);

        problems.ShouldBeEmpty();
    }

    [Fact]
    public void Given_Several_Events_With_Ambiguous_Strategies_When_Validating_Then_Reports_Each_Event_And_Only_Unmatched_Declarations_As_Unused()
    {
        IReadOnlyList<RequestFlowValidationProblem> problems = Validate(
            [typeof(MultiInterfaceEvent), typeof(TestEvent), typeof(AnotherMultiInterfaceEvent)],
            [
                Strategy(typeof(IFirstEvent), typeof(StrategyA)),
                Strategy(typeof(ISecondEvent), typeof(StrategyB)),
                Strategy(typeof(IEvent), typeof(StrategyA)),
                Strategy(typeof(IUnreachedEvent), typeof(StrategyA)),
            ],
            unusedStrategiesDisallowed: true);

        problems.Select(problem => problem.Code).ShouldBe(["RF0120", "RF0120", "RF0122"]);
        problems.Select(problem => problem.Subject).ShouldBe([
            typeof(MultiInterfaceEvent),
            typeof(AnotherMultiInterfaceEvent),
            typeof(IUnreachedEvent),
        ]);
        foreach (RequestFlowValidationProblem problem in problems.Take(2))
        {
            problem.Message.ShouldContain(typeof(IFirstEvent).FullName!);
            problem.Message.ShouldContain(typeof(ISecondEvent).FullName!);
        }
    }

    [Fact]
    public void Given_A_Global_Declaration_With_No_Events_When_Unused_Declarations_Are_Disallowed_Then_Reports_Nothing()
    {
        IReadOnlyList<RequestFlowValidationProblem> problems = Validate(
            [],
            [Strategy(null, typeof(StrategyA))],
            unusedStrategiesDisallowed: true);

        problems.ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Strategy_And_Event_Handler_With_Different_Lifetimes_When_Validating_Then_Reports_RF0123()
    {
        RequestFlowModel roles = new RequestFlowModelBuilder()
            .AddEvent(typeof(TestEvent))
            .AddEventHandler(
                typeof(DualRoleStrategy),
                typeof(TestEvent),
                RequestFlowLifetime.Transient)
            .Build();

        RequestFlowValidationProblem problem = Validate(
            [typeof(TestEvent)],
            [Strategy(null, typeof(DualRoleStrategy), RequestFlowLifetime.Singleton)],
            roles: roles).ShouldHaveSingleItem();

        problem.Code.ShouldBe("RF0123");
        problem.Subject.ShouldBe(typeof(DualRoleStrategy));
        problem.Message.ShouldContain("Singleton");
        problem.Message.ShouldContain("Transient");
    }

    [Fact]
    public void Given_A_Strategy_And_Event_Handler_With_The_Same_Lifetime_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowModel roles = new RequestFlowModelBuilder()
            .AddEvent(typeof(TestEvent))
            .AddEventHandler(
                typeof(DualRoleStrategy),
                typeof(TestEvent),
                RequestFlowLifetime.Singleton)
            .Build();

        IReadOnlyList<RequestFlowValidationProblem> problems = Validate(
            [typeof(TestEvent)],
            [Strategy(null, typeof(DualRoleStrategy), RequestFlowLifetime.Singleton)],
            roles: roles);

        problems.ShouldBeEmpty();
    }

    // Request and stream handlers register under the handler interface, not the concrete class,
    // so their descriptors never compete with the strategy's and no lifetime can be masked.
    [Fact]
    public void Given_A_Strategy_And_Request_Handler_With_Different_Lifetimes_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowModel roles = new RequestFlowModelBuilder()
            .AddRequest(
                typeof(TestRequest),
                request => request.AddHandler(
                    typeof(DualRoleStrategy),
                    RequestFlowLifetime.Transient))
            .Build();

        IReadOnlyList<RequestFlowValidationProblem> problems = Validate(
            [typeof(TestEvent)],
            [Strategy(null, typeof(DualRoleStrategy), RequestFlowLifetime.Singleton)],
            roles: roles);

        problems.ShouldBeEmpty();
    }

    [Fact]
    public void Given_One_Equal_Role_And_One_Different_Role_When_Validating_Then_Reports_RF0123()
    {
        RequestFlowModel roles = new RequestFlowModelBuilder()
            .AddRequest(
                typeof(TestRequest),
                request => request.AddHandler(
                    typeof(DualRoleStrategy),
                    RequestFlowLifetime.Singleton))
            .AddEvent(typeof(TestEvent))
            .AddEventHandler(
                typeof(DualRoleStrategy),
                typeof(TestEvent),
                RequestFlowLifetime.Transient)
            .Build();

        RequestFlowValidationProblem problem = Validate(
            [typeof(TestEvent)],
            [Strategy(null, typeof(DualRoleStrategy), RequestFlowLifetime.Singleton)],
            roles: roles).ShouldHaveSingleItem();

        problem.Code.ShouldBe("RF0123");
        problem.Message.ShouldContain("Transient");
    }

    #region Helpers

    private static IReadOnlyList<RequestFlowValidationProblem> Validate(
        Type[] eventTypes,
        EventStrategyInput[] strategies,
        bool unusedStrategiesDisallowed = false,
        RequestFlowModel? roles = null)
    {
        EventClosureResult closure = EventClosure.Build(eventTypes, [], strategies);
        roles ??= new RequestFlowModelBuilder().Build();
        var model = new RequestFlowModel(
            roles.Requests.ToArray(),
            roles.StageDeclarations.ToArray(),
            closure.Events,
            roles.EventSubscriptions.ToArray(),
            closure.EventStrategies);
        var context = new RequestFlowValidationContext(
            model,
            unhandledRequestsAllowed: false,
            unusedStagesDisallowed: false,
            unhandledEventsAllowed: false,
            unusedEventHandlersDisallowed: unusedStrategiesDisallowed);

        return new EventStrategyRule(closure.StrategyResolution).Validate(context).ToArray();
    }

    private static EventStrategyInput Strategy(
        Type? declaredEventType,
        Type strategyType,
        RequestFlowLifetime lifetime = RequestFlowLifetime.Singleton)
        => new(declaredEventType, strategyType, lifetime);

    private sealed record TestEvent : IEvent;

    private sealed class TestRequest;

    private interface IFirstEvent : IEvent
    { }

    private interface ISecondEvent : IEvent
    { }

    private interface IDerivedEvent : IFirstEvent
    { }

    private interface IUnreachedEvent : IEvent
    { }

    private sealed record MultiInterfaceEvent : IDerivedEvent, ISecondEvent;

    private sealed record AnotherMultiInterfaceEvent : IFirstEvent, ISecondEvent;

    private interface IInvalidStrategy : IEventPublishStrategy
    { }

    private abstract class AbstractStrategy : IEventPublishStrategy
    {
        public Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class StrategyA : AbstractStrategy;

    private sealed class StrategyB : AbstractStrategy;

    private sealed class DualRoleStrategy : IEventPublishStrategy, IEventHandler<TestEvent>
    {
        public Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    #endregion
}
