using RequestFlow;
using RequestFlow.Tests.ValidationFixtures;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class ValueValidationIntegrationTests
{
    [Fact]
    public void Given_Two_Value_Handlers_When_The_Generic_Duplicate_Rule_Validates_Then_Reports_RF0101()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(DuplicateValue), request => request
                .AddHandler(
                    typeof(FirstDuplicateValueHandler),
                    typeof(string),
                    typeof(IValueRequestHandler<,>))
                .AddHandler(
                    typeof(SecondDuplicateValueHandler),
                    typeof(string),
                    typeof(IValueRequestHandler<,>)))
            .BuildContext();

        RequestFlowValidationProblem problem = new DuplicateHandlerRule()
            .Validate(context)
            .ShouldHaveSingleItem();

        problem.Code.ShouldBe(ProblemCodes.DuplicateHandler);
        problem.Subject.ShouldBe(typeof(DuplicateValue));
    }

    [Fact]
    public void Given_An_Unhandled_Concrete_Value_Request_When_The_Generic_Unhandled_Rule_Validates_Then_Reports_RF0102()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StageValue))
            .BuildContext();

        RequestFlowValidationProblem problem = new UnhandledRequestRule()
            .Validate(context)
            .ShouldHaveSingleItem();

        problem.Code.ShouldBe(ProblemCodes.UnhandledRequest);
        problem.Subject.ShouldBe(typeof(StageValue));
    }

    [Fact]
    public void Given_Aliased_Value_Stages_When_The_Generic_Alias_Rule_Validates_Then_Reports_RF0104()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(AliasedValue), request => request
                .AddHandler(
                    typeof(AliasedValueHandler),
                    typeof(string),
                    typeof(IValueRequestHandler<,>))
                .AddStage(
                    typeof(AliasedValueStage<>),
                    typeof(AliasedValueStage<AliasedValue>),
                    typeof(IValueRequestStage<,>))
                .AddStage(
                    typeof(AliasedValueStage<AliasedValue>),
                    typeof(AliasedValueStage<AliasedValue>),
                    typeof(IValueRequestStage<,>)))
            .BuildContext();

        RequestFlowValidationProblem problem = new AliasedStageRule()
            .Validate(context)
            .ShouldHaveSingleItem();

        problem.Code.ShouldBe(ProblemCodes.AliasedStage);
        problem.Subject.ShouldBe(typeof(AliasedValueStage<>));
        problem.Message.ShouldContain("AddValueStage");
    }

    [Fact]
    public void Given_An_Unused_Value_Stage_When_The_Opted_In_Generic_Rule_Validates_Then_Reports_RF0105()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StageValue), request => request.AddHandler(
                typeof(StageValueHandler),
                typeof(string),
                typeof(IValueRequestHandler<,>)))
            .AddStageDeclaration(typeof(UnusedValueStage<>), typeof(IValueRequestStage<,>))
            .BuildContext(unusedStagesDisallowed: true);

        RequestFlowValidationProblem problem = new UnusedStageRule()
            .Validate(context)
            .ShouldHaveSingleItem();

        context.UnusedStagesDisallowed.ShouldBeTrue();
        problem.Code.ShouldBe(ProblemCodes.UnusedStage);
        problem.Subject.ShouldBe(typeof(UnusedValueStage<>));
    }

    [Fact]
    public void Given_A_Value_Stage_That_Is_A_Longer_Lived_Event_Handler_When_The_Generic_Lifetime_Rule_Validates_Then_Reports_RF0118()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(DualRoleValue), request => request
                .AddHandler(
                    typeof(DualRoleValueHandler),
                    typeof(string),
                    typeof(IValueRequestHandler<,>))
                .AddStage(
                    typeof(DualRoleValueStageEventHandler),
                    typeof(DualRoleValueStageEventHandler),
                    typeof(IValueRequestStage<,>)))
            .AddStageDeclaration(
                typeof(DualRoleValueStageEventHandler),
                RequestFlowLifetime.Singleton,
                typeof(IValueRequestStage<,>))
            .AddEvent(typeof(DualRoleEvent))
            .AddEventHandler(
                typeof(DualRoleValueStageEventHandler),
                typeof(DualRoleEvent),
                RequestFlowLifetime.Transient)
            .BuildContext();

        RequestFlowValidationProblem problem = new StageEventHandlerLifetimeRule()
            .Validate(context)
            .ShouldHaveSingleItem();

        problem.Code.ShouldBe(ProblemCodes.StageEventHandlerLifetime);
        problem.Subject.ShouldBe(typeof(DualRoleValueStageEventHandler));
    }

    [Fact]
    public void Given_A_Non_Concrete_Value_Request_When_The_Generic_Shape_Rule_Validates_Then_Reports_RF0124()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(NonConcreteValue), request => request.AddHandler(
                typeof(NonConcreteValueHandler),
                typeof(string),
                typeof(IValueRequestHandler<,>)))
            .BuildContext();

        RequestFlowValidationProblem problem = new NonConcreteRequestRule()
            .Validate(context)
            .ShouldHaveSingleItem();

        problem.Code.ShouldBe(ProblemCodes.NonConcreteRequest);
        problem.Subject.ShouldBe(typeof(NonConcreteValueHandler));
    }

    #region Helpers

    private abstract record DuplicateValue : IValueRequest<string>;

    private abstract class FirstDuplicateValueHandler
        : IValueRequestHandler<DuplicateValue, string>
    {
        public abstract ValueTask<string> HandleAsync(
            DuplicateValue request,
            CancellationToken cancellationToken);
    }

    private abstract class SecondDuplicateValueHandler
        : IValueRequestHandler<DuplicateValue, string>
    {
        public abstract ValueTask<string> HandleAsync(
            DuplicateValue request,
            CancellationToken cancellationToken);
    }

    private abstract record AliasedValue : IValueRequest<string>;

    private abstract class AliasedValueHandler : IValueRequestHandler<AliasedValue, string>
    {
        public abstract ValueTask<string> HandleAsync(
            AliasedValue request,
            CancellationToken cancellationToken);
    }

    private abstract class AliasedValueStage<TRequest>
        : IValueRequestStage<TRequest, string>
        where TRequest : IValueRequest<string>
    {
        public abstract ValueTask<string> HandleAsync(
            TRequest request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken);
    }

    private abstract class UnusedValueStage<TRequest>
        : IValueRequestStage<TRequest, int>
        where TRequest : IValueRequest<int>
    {
        public abstract ValueTask<int> HandleAsync(
            TRequest request,
            ValueContinuation<int> next,
            CancellationToken cancellationToken);
    }

    private abstract record DualRoleValue : IValueRequest<string>;

    private abstract class DualRoleValueHandler : IValueRequestHandler<DualRoleValue, string>
    {
        public abstract ValueTask<string> HandleAsync(
            DualRoleValue request,
            CancellationToken cancellationToken);
    }

    private sealed record DualRoleEvent : IEvent;

    private abstract class DualRoleValueStageEventHandler
        : IValueRequestStage<DualRoleValue, string>, IEventHandler<DualRoleEvent>
    {
        public abstract ValueTask<string> HandleAsync(
            DualRoleValue request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken);

        public abstract Task HandleAsync(
            DualRoleEvent @event,
            CancellationToken cancellationToken);
    }

    private abstract record NonConcreteValue : IValueRequest<string>;

    private abstract class NonConcreteValueHandler
        : IValueRequestHandler<NonConcreteValue, string>
    {
        public abstract ValueTask<string> HandleAsync(
            NonConcreteValue request,
            CancellationToken cancellationToken);
    }

    #endregion
}
