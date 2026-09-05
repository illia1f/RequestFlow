using Microsoft.Extensions.DependencyInjection;
using RequestFlow;
using RequestFlow.Tests.ValidationFixtures;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class ValueContractConflictRuleTests
{
    [Fact]
    public void Given_Task_And_Value_Contracts_When_Validating_Then_Reports_RF0126()
    {
        RequestFlowValidationContext context = Context(typeof(MixedTaskValue));
        var sut = new ContractConflictRule(
            MessageContract.Request,
            MessageContract.ValueRequest,
            ProblemCodes.RequestAndValueRequest);

        RequestFlowValidationProblem problem = sut.Validate(context).ShouldHaveSingleItem();

        problem.Code.ShouldBe(ProblemCodes.RequestAndValueRequest);
        problem.Subject.ShouldBe(typeof(MixedTaskValue));
        problem.Message.ShouldContain("IRequest");
        problem.Message.ShouldContain("IValueRequest");
    }

    [Fact]
    public void Given_Stream_And_Value_Contracts_When_Validating_Then_Reports_RF0127()
    {
        RequestFlowValidationContext context = Context(typeof(MixedStreamValue));
        var sut = new ContractConflictRule(
            MessageContract.StreamRequest,
            MessageContract.ValueRequest,
            ProblemCodes.StreamRequestAndValueRequest);

        RequestFlowValidationProblem problem = sut.Validate(context).ShouldHaveSingleItem();

        problem.Code.ShouldBe(ProblemCodes.StreamRequestAndValueRequest);
        problem.Subject.ShouldBe(typeof(MixedStreamValue));
        problem.Message.ShouldContain("IStreamRequest");
        problem.Message.ShouldContain("IValueRequest");
    }

    [Fact]
    public void Given_Value_And_Event_Contracts_When_Validating_Then_Reports_RF0128()
    {
        RequestFlowValidationContext context = Context(typeof(MixedValueEvent));
        var sut = new ContractConflictRule(
            MessageContract.ValueRequest,
            MessageContract.Event,
            ProblemCodes.ValueRequestAndEvent);

        RequestFlowValidationProblem problem = sut.Validate(context).ShouldHaveSingleItem();

        problem.Code.ShouldBe(ProblemCodes.ValueRequestAndEvent);
        problem.Subject.ShouldBe(typeof(MixedValueEvent));
        problem.Message.ShouldContain("IValueRequest");
        problem.Message.ShouldContain("IEvent");
    }

    [Fact]
    public void Given_All_Four_Contract_Families_When_Freezing_Then_All_Six_Conflicts_Follow_Built_In_Order()
    {
        RequestFlowValidationException exception = ValidateFixtures();

        exception.Problems
            .Where(problem => problem.Subject == typeof(AllValueMessageContracts))
            .Select(problem => problem.Code)
            .ShouldBe([
                ProblemCodes.RequestAndStreamRequest,
                ProblemCodes.RequestAndValueRequest,
                ProblemCodes.StreamRequestAndValueRequest,
                ProblemCodes.RequestAndEvent,
                ProblemCodes.StreamRequestAndEvent,
                ProblemCodes.ValueRequestAndEvent,
            ]);
    }

    [Fact]
    public void Given_A_Plain_Value_Request_When_Validating_Conflicts_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = Context(typeof(PlainValue));
        IRequestFlowValidationRule[] rules =
        [
            new ContractConflictRule(
                MessageContract.Request,
                MessageContract.ValueRequest,
                ProblemCodes.RequestAndValueRequest),
            new ContractConflictRule(
                MessageContract.StreamRequest,
                MessageContract.ValueRequest,
                ProblemCodes.StreamRequestAndValueRequest),
            new ContractConflictRule(
                MessageContract.ValueRequest,
                MessageContract.Event,
                ProblemCodes.ValueRequestAndEvent),
        ];

        rules.SelectMany(rule => rule.Validate(context)).ShouldBeEmpty();
    }

    [Fact]
    public void Given_Task_And_Value_Contracts_With_Wider_Task_Shapes_When_Validating_Then_Only_RF0126_Is_Reported()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(MixedTaskValue), request => request.AddHandler(
                typeof(WideTaskHandler),
                typeof(object),
                typeof(IRequestHandler<,>)))
            .AddStageDeclaration(typeof(WideTaskStage))
            .BuildContext();
        IRequestFlowValidationRule[] rules =
        [
            new ContractConflictRule(
                MessageContract.Request,
                MessageContract.ValueRequest,
                ProblemCodes.RequestAndValueRequest),
            new HandlerResponseMismatchRule(),
            new StageResponseMismatchRule(),
        ];

        List<RequestFlowValidationProblem> problems =
            [.. rules.SelectMany(rule => rule.Validate(context))];

        problems.ShouldHaveSingleItem().Code.ShouldBe(ProblemCodes.RequestAndValueRequest);
        problems.ShouldNotContain(problem => problem.Code == ProblemCodes.HandlerResponseMismatch);
        problems.ShouldNotContain(problem => problem.Code == ProblemCodes.StageResponseMismatch);
    }

    [Fact]
    public void Given_Stream_And_Value_Contracts_With_Wider_Stream_Shapes_When_Validating_Then_Only_RF0127_Is_Reported()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(MixedStreamValue), request => request
                .AddHandler(
                    typeof(WideStreamHandler),
                    typeof(object),
                    typeof(IStreamRequestHandler<,>)))
            .AddStageDeclaration(typeof(WideStreamStage), typeof(IStreamRequestStage<,>))
            .BuildContext();
        IRequestFlowValidationRule[] rules =
        [
            new ContractConflictRule(
                MessageContract.StreamRequest,
                MessageContract.ValueRequest,
                ProblemCodes.StreamRequestAndValueRequest),
            new StreamRequestContractRule(),
            new StreamStageItemMismatchRule(),
        ];

        List<RequestFlowValidationProblem> problems =
            [.. rules.SelectMany(rule => rule.Validate(context))];

        problems.ShouldHaveSingleItem().Code.ShouldBe(ProblemCodes.StreamRequestAndValueRequest);
        problems.ShouldNotContain(problem => problem.Code == ProblemCodes.StreamItemMismatch);
        problems.ShouldNotContain(problem => problem.Code == ProblemCodes.StreamStageItemMismatch);
    }

    [Fact]
    public void Given_The_Validation_Fixtures_When_Freezing_Then_Each_Value_Pairwise_Conflict_Is_Aggregated()
    {
        RequestFlowValidationException exception = ValidateFixtures();

        exception.Problems.ShouldContain(problem =>
            problem.Code == ProblemCodes.RequestAndValueRequest
            && problem.Subject == typeof(RequestFlow.Tests.ValidationFixtures.MixedRequestValue));
        exception.Problems.ShouldContain(problem =>
            problem.Code == ProblemCodes.StreamRequestAndValueRequest
            && problem.Subject == typeof(RequestFlow.Tests.ValidationFixtures.MixedStreamValue));
        exception.Problems.ShouldContain(problem =>
            problem.Code == ProblemCodes.ValueRequestAndEvent
            && problem.Subject == typeof(ValueEventConflict));
    }

    #region Helpers

    private static RequestFlowValidationContext Context(Type requestType)
        => new RequestFlowModelBuilder().AddRequest(requestType).BuildContext();

    private static RequestFlowValidationException ValidateFixtures()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options => options.RegisterHandlersFromAssembly(
            typeof(AllValueMessageContracts).Assembly));
        using ServiceProvider provider = services.BuildServiceProvider();

        return Should.Throw<RequestFlowValidationException>(
            () => provider.GetRequiredService<IValueRequestDispatcher>());
    }

    private abstract record MixedTaskValue : IRequest<string>, IValueRequest<int>;

    private abstract record MixedStreamValue : IStreamRequest<string>, IValueRequest<int>;

    private abstract record MixedValueEvent : IValueRequest<int>, IEvent;

    private abstract record PlainValue : IValueRequest<int>;

    private abstract class WideTaskHandler : IRequestHandler<MixedTaskValue, object>
    {
        public abstract Task<object> HandleAsync(
            MixedTaskValue request,
            CancellationToken cancellationToken);
    }

    private abstract class WideTaskStage : IRequestStage<MixedTaskValue, object>
    {
        public abstract Task<object> HandleAsync(
            MixedTaskValue request,
            Continuation<object> next,
            CancellationToken cancellationToken);
    }

    private abstract class WideStreamHandler : IStreamRequestHandler<MixedStreamValue, object>
    {
        public abstract IAsyncEnumerable<object> Handle(
            MixedStreamValue request,
            CancellationToken cancellationToken);
    }

    private abstract class WideStreamStage : IStreamRequestStage<MixedStreamValue, object>
    {
        public abstract IAsyncEnumerable<object> Handle(
            MixedStreamValue request,
            StreamContinuation<object> next,
            CancellationToken cancellationToken);
    }

    #endregion
}
