using Microsoft.Extensions.DependencyInjection;
using RequestFlow.Tests.ValidationFixtures;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class EventValidationTests
{
    [Fact]
    public void Given_An_Event_Without_A_Handler_When_Validating_Then_Reports_The_Event()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddEvent(typeof(UnhandledEvent))
            .BuildContext();

        RequestFlowValidationProblem problem =
            new UnhandledEventRule().Validate(context).ShouldHaveSingleItem();

        problem.Code.ShouldBe("RF0114");
        problem.Subject.ShouldBe(typeof(UnhandledEvent));
        problem.Message.ShouldContain(typeof(UnhandledEvent).FullName!);
        problem.Message.ShouldContain("no handler");
    }

    [Fact]
    public void Given_An_Event_With_A_Handler_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddEvent(typeof(HandledEvent))
            .AddEventHandler(typeof(HandledEventHandler), typeof(HandledEvent))
            .BuildContext();

        new UnhandledEventRule().Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_Two_Dead_Subscriptions_From_One_Handler_When_Validating_Then_Reports_Each_Subscription()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddEventHandler(typeof(DeadSubscriptionHandler), typeof(BaseEvent))
            .AddEventHandler(typeof(DeadSubscriptionHandler), typeof(IDeadEvent))
            .BuildContext();

        List<RequestFlowValidationProblem> problems =
            [.. new UnusedEventSubscriptionRule().Validate(context)];

        problems.Count.ShouldBe(2);
        problems.ShouldAllBe(problem =>
            problem.Code == "RF0115"
            && problem.Subject == typeof(DeadSubscriptionHandler)
            && problem.Message.Contains("subscription"));
        problems[0].Message.ShouldContain(typeof(BaseEvent).FullName!);
        problems[1].Message.ShouldContain(typeof(IDeadEvent).FullName!);
    }

    [Fact]
    public void Given_A_Subscription_That_Reaches_An_Event_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddEvent(typeof(DerivedEvent))
            .AddEventHandler(typeof(BaseEventHandler), typeof(BaseEvent))
            .BuildContext();

        new UnusedEventSubscriptionRule().Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_An_Event_Only_Model_With_A_Request_Event_Type_When_Validating_Then_Reports_The_Conflict()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddEvent(typeof(RequestEvent<int>))
            .BuildContext();
        var sut = new ContractConflictRule(
            MessageContract.Request,
            MessageContract.Event,
            ProblemCodes.RequestAndEvent);

        RequestFlowValidationProblem problem = sut.Validate(context).ShouldHaveSingleItem();

        problem.Code.ShouldBe("RF0116");
        problem.Subject.ShouldBe(typeof(RequestEvent<int>));
        problem.Message.ShouldContain("IRequest");
        problem.Message.ShouldContain("IEvent");
    }

    [Fact]
    public void Given_An_Event_Only_Model_With_A_Stream_Event_Type_When_Validating_Then_Reports_The_Conflict()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddEvent(typeof(StreamEvent<int>))
            .BuildContext();
        var sut = new ContractConflictRule(
            MessageContract.StreamRequest,
            MessageContract.Event,
            ProblemCodes.StreamRequestAndEvent);

        RequestFlowValidationProblem problem = sut.Validate(context).ShouldHaveSingleItem();

        problem.Code.ShouldBe("RF0117");
        problem.Subject.ShouldBe(typeof(StreamEvent<int>));
        problem.Message.ShouldContain("IStreamRequest");
        problem.Message.ShouldContain("IEvent");
    }

    [Fact]
    public void Given_An_All_Contract_Type_In_Both_Model_Lists_When_Validating_Then_Each_Pair_Is_Reported_Once()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(AllContracts<int>))
            .AddEvent(typeof(AllContracts<int>))
            .BuildContext();
        IRequestFlowValidationRule[] rules =
        [
            new ContractConflictRule(
                MessageContract.Request,
                MessageContract.StreamRequest,
                ProblemCodes.RequestAndStreamRequest),
            new ContractConflictRule(
                MessageContract.Request,
                MessageContract.Event,
                ProblemCodes.RequestAndEvent),
            new ContractConflictRule(
                MessageContract.StreamRequest,
                MessageContract.Event,
                ProblemCodes.StreamRequestAndEvent),
        ];

        List<RequestFlowValidationProblem> problems =
            [.. rules.SelectMany(rule => rule.Validate(context))];

        problems.Select(problem => problem.Code).ShouldBe(["RF0109", "RF0116", "RF0117"]);
        problems.ShouldAllBe(problem => problem.Subject == typeof(AllContracts<int>));
    }

    [Fact]
    public void Given_A_Hand_Built_Event_Model_When_Reading_Handlers_Then_Order_Matches_The_Event_Closure()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddEvent(typeof(OrderedEvent))
            .AddEventHandler(typeof(UniversalHandler), typeof(IEvent))
            .AddEventHandler(typeof(InterfaceHandler), typeof(IOrderedEvent))
            .AddEventHandler(typeof(BaseHandler), typeof(OrderedEventBase))
            .AddEventHandler(typeof(ExactHandler), typeof(OrderedEvent))
            .Build();

        model.Events.ShouldHaveSingleItem().Handlers
            .Select(handler => handler.HandlerType)
            .ShouldBe([
                typeof(ExactHandler),
                typeof(BaseHandler),
                typeof(InterfaceHandler),
                typeof(UniversalHandler),
            ]);
    }

    [Fact]
    public void Given_Dead_Subscriptions_With_Default_Options_When_Validating_Then_Unused_Subscription_Is_Not_Reported()
    {
        RequestFlowValidationException exception = ValidateFixtures();

        exception.Problems.ShouldNotContain(problem => problem.Code == "RF0115");
    }

    [Fact]
    public void Given_Dead_Subscriptions_When_Disallowed_Then_Each_Contract_Is_Reported()
    {
        RequestFlowValidationException exception = ValidateFixtures(
            options => options.DisallowUnusedEventHandlers());

        RequestFlowValidationProblem[] problems = [.. exception.Problems.Where(problem =>
            problem.Code == "RF0115"
            && problem.Subject == typeof(DeadValidationEventHandler))];
        problems.Length.ShouldBe(2);
        problems.ShouldContain(problem =>
            problem.Message.Contains(typeof(DeadValidationEventBase).FullName!));
        problems.ShouldContain(problem =>
            problem.Message.Contains(typeof(IDeadValidationEvent).FullName!));
    }

    [Fact]
    public void Given_Unhandled_Events_Allowed_And_Dead_Subscriptions_Disallowed_When_Validating_Then_Unused_Subscription_Is_Still_Reported()
    {
        RequestFlowValidationException exception = ValidateFixtures(options => options
            .AllowAllUnhandledEvents()
            .DisallowUnusedEventHandlers());

        exception.Problems.ShouldContain(problem =>
            problem.Code == "RF0115"
            && problem.Subject == typeof(DeadValidationEventHandler));
    }

    [Fact]
    public void Given_Unhandled_Events_Allowed_When_Validating_Then_Unhandled_Event_Is_Not_Reported()
    {
        RequestFlowValidationException exception = ValidateFixtures(
            options => options.AllowAllUnhandledEvents());

        exception.Problems.ShouldNotContain(problem => problem.Code == "RF0114");
    }

    [Fact]
    public void Given_An_Unhandled_Event_Fixture_When_Validating_Then_Reports_The_Event()
    {
        RequestFlowValidationException exception = ValidateFixtures();

        exception.Problems.ShouldContain(problem =>
            problem.Code == "RF0114"
            && problem.Subject == typeof(UnhandledValidationEvent));
    }

    [Fact]
    public void Given_A_Request_Event_Fixture_When_Validating_Then_Reports_The_Conflict()
    {
        RequestFlowValidationException exception = ValidateFixtures();

        exception.Problems.ShouldContain(problem =>
            problem.Code == "RF0116"
            && problem.Subject == typeof(RequestEventConflict));
    }

    [Fact]
    public void Given_A_Stream_Event_Fixture_When_Validating_Then_Reports_The_Conflict()
    {
        RequestFlowValidationException exception = ValidateFixtures();

        exception.Problems.ShouldContain(problem =>
            problem.Code == "RF0117"
            && problem.Subject == typeof(StreamEventConflict));
    }

    [Fact]
    public void Given_A_Handled_Closed_Generic_Event_Fixture_When_Validating_Then_Does_Not_Report_The_Event_As_Unhandled()
    {
        RequestFlowValidationException exception = ValidateFixtures();

        exception.Problems.ShouldNotContain(problem =>
            problem.Code == "RF0114"
            && problem.Subject == typeof(ClosedGenericValidationEvent<int>));
    }

    [Fact]
    public void Given_An_All_Contract_Fixture_When_Validating_Then_Conflict_Codes_Follow_Built_In_Order()
    {
        RequestFlowValidationException exception = ValidateFixtures();

        exception.Problems
            .Where(problem => problem.Subject == typeof(AllMessageContracts))
            .Select(problem => problem.Code)
            .ShouldBe(["RF0109", "RF0116", "RF0117"]);
    }

    #region Helpers

    private static RequestFlowValidationException ValidateFixtures(
        Action<RequestFlowOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options =>
        {
            options.RegisterHandlersFromAssembly(typeof(UnhandledValidationEvent).Assembly);
            configure?.Invoke(options);
        });
        using ServiceProvider provider = services.BuildServiceProvider();

        return Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());
    }

    private sealed record UnhandledEvent : IEvent;

    private sealed record HandledEvent : IEvent;

    private sealed class HandledEventHandler
    { }

    private abstract record BaseEvent : IEvent;

    private interface IDeadEvent : IEvent
    { }

    private sealed record DerivedEvent : BaseEvent;

    private sealed class DeadSubscriptionHandler
    { }

    private sealed class BaseEventHandler
    { }

    private sealed record RequestEvent<T> : IRequest<int>, IEvent;

    private sealed record StreamEvent<T> : IStreamRequest<int>, IEvent;

    private sealed record AllContracts<T> : IRequest<int>, IStreamRequest<int>, IEvent;

    private interface IOrderedEvent : IEvent
    { }

    private abstract record OrderedEventBase : IEvent;

    private sealed record OrderedEvent : OrderedEventBase, IOrderedEvent;

    private sealed class ExactHandler
    { }

    private sealed class BaseHandler
    { }

    private sealed class InterfaceHandler
    { }

    private sealed class UniversalHandler
    { }

    #endregion
}
