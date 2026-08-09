using RequestFlow.Tests.ValidationFixtures;

namespace RequestFlow.Cqrs.Tests.Unit;

public sealed class CommandQuerySplitRuleTests
{
    [Fact]
    public void Given_A_Request_That_Is_Command_And_Query_When_Validating_Then_Reports_The_Type()
    {
        RequestFlowValidationContext context = Context(typeof(Confused));

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("CQRS0001");
        problem.Subject.ShouldBe(typeof(Confused));
        problem.Message.ShouldContain("both a command and a query");
        problem.Message.ShouldContain("pick one side of the split");
    }

    [Fact]
    public void Given_A_Void_Command_That_Is_Also_A_Query_When_Validating_Then_Reports_The_Type()
    {
        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(Context(typeof(VoidConfused)))];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("CQRS0001");
        problem.Subject.ShouldBe(typeof(VoidConfused));
    }

    [Fact]
    public void Given_A_Plain_Command_When_Validating_Then_Reports_Nothing()
    {
        _sut.Validate(Context(typeof(PlainCommand))).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Plain_Query_When_Validating_Then_Reports_Nothing()
    {
        _sut.Validate(Context(typeof(PlainQuery))).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Plain_Request_When_Validating_Then_Reports_Nothing()
    {
        _sut.Validate(Context(typeof(PlainRequest))).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Split_Request_Among_Clean_Ones_When_Validating_Then_Only_The_Split_One_Is_Reported()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(PlainCommand))
            .AddRequest(typeof(Confused))
            .AddRequest(typeof(PlainQuery))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        problems.ShouldHaveSingleItem().Subject.ShouldBe(typeof(Confused));
    }

    // The rule reads the model off the context and nothing else, so the registration opt-ins leave
    // its finding alone.
    [Fact]
    public void Given_Both_Registration_Opt_Ins_When_Validating_Then_The_Split_Is_Still_Reported()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(Confused))
            .BuildContext(unhandledRequestsAllowed: true, unusedStagesDisallowed: true);

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        problems.ShouldHaveSingleItem().Code.ShouldBe("CQRS0001");
    }

    #region Initialization

    private readonly CommandQuerySplitRule _sut = new();

    #endregion

    #region Helpers

    private static RequestFlowValidationContext Context(Type requestType)
        => new RequestFlowModelBuilder().AddRequest(requestType).BuildContext();

    // Confused and VoidConfused stay in the fixtures assembly: a split request declared here
    // would fail AddCqrsTests's freeze of this assembly.
    private sealed record PlainCommand : ICommand<int>;

    private sealed record PlainQuery : IQuery<int>;

    private sealed record PlainRequest : IRequest<int>;

    // Handled because AddCqrsTests freezes this assembly without AllowUnhandledRequests.
    private sealed class PlainCommandHandler : IRequestHandler<PlainCommand, int>
    {
        public Task<int> HandleAsync(PlainCommand request, CancellationToken cancellationToken)
            => Task.FromResult(0);
    }

    private sealed class PlainQueryHandler : IRequestHandler<PlainQuery, int>
    {
        public Task<int> HandleAsync(PlainQuery request, CancellationToken cancellationToken)
            => Task.FromResult(0);
    }

    private sealed class PlainRequestHandler : IRequestHandler<PlainRequest, int>
    {
        public Task<int> HandleAsync(PlainRequest request, CancellationToken cancellationToken)
            => Task.FromResult(0);
    }

    #endregion
}
