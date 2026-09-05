using System.Runtime.CompilerServices;
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
    public void Given_A_Command_That_Is_Also_A_Stream_Query_When_Validating_Then_Reports_The_Type()
    {
        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(Context(typeof(StreamConfused)))];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("CQRS0001");
        problem.Subject.ShouldBe(typeof(StreamConfused));
        problem.Message.ShouldContain("both a command and a stream query");
        problem.Message.ShouldContain("pick one side of the split");
    }

    // Both contracts sit on the query side, so mixing them is RF0109's business, not this rule's.
    [Fact]
    public void Given_A_Query_That_Is_Also_A_Stream_Query_When_Validating_Then_Reports_Nothing()
    {
        _sut.Validate(Context(typeof(MixedCqrsFamilies))).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Plain_Stream_Query_When_Validating_Then_Reports_Nothing()
    {
        _sut.Validate(Context(typeof(PlainStreamQuery))).ShouldBeEmpty();
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

    [Theory]
    [InlineData(typeof(ValueCommandAndValueQuery))]
    [InlineData(typeof(TaskCommandAndValueQuery))]
    [InlineData(typeof(ValueCommandAndTaskQuery))]
    [InlineData(typeof(ValueCommandAndStreamQuery))]
    public void Given_A_Command_And_Query_Side_When_Validating_Then_Reports_CQRS0001(
        Type requestType)
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(requestType)
            .BuildContext();

        RequestFlowValidationProblem problem = _sut
            .Validate(context)
            .ShouldHaveSingleItem();

        problem.Code.ShouldBe(CqrsProblemCodes.CommandQuerySplit);
    }

    [Fact]
    public void Given_A_Value_And_Stream_Query_When_Validating_Then_Reports_Nothing()
    {
        _sut.Validate(Context(typeof(ValueQueryAndStreamQuery))).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Plain_Value_Command_When_Validating_Then_Reports_Nothing()
    {
        _sut.Validate(Context(typeof(PlainValueCommand))).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Plain_Value_Query_When_Validating_Then_Reports_Nothing()
    {
        _sut.Validate(Context(typeof(PlainValueQuery))).ShouldBeEmpty();
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

    private sealed record PlainStreamQuery : IStreamQuery<int>;

    private sealed record PlainRequest : IRequest<int>;

    private abstract class ValueCommandAndValueQuery
        : IValueCommand<int>, IValueQuery<int>
    { }

    private abstract class TaskCommandAndValueQuery
        : ICommand<int>, IValueQuery<int>
    { }

    private abstract class ValueCommandAndTaskQuery
        : IValueCommand<int>, IQuery<int>
    { }

    private abstract class ValueCommandAndStreamQuery
        : IValueCommand<int>, IStreamQuery<int>
    { }

    private abstract class ValueQueryAndStreamQuery
        : IValueQuery<int>, IStreamQuery<int>
    { }

    private sealed record PlainValueCommand : IValueCommand<int>;

    private sealed record PlainValueQuery : IValueQuery<int>;

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

    private sealed class PlainStreamQueryHandler : IStreamQueryHandler<PlainStreamQuery, int>
    {
        public async IAsyncEnumerable<int> Handle(
            PlainStreamQuery request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return 1;
        }
    }

    private sealed class PlainRequestHandler : IRequestHandler<PlainRequest, int>
    {
        public Task<int> HandleAsync(PlainRequest request, CancellationToken cancellationToken)
            => Task.FromResult(0);
    }

    private sealed class PlainValueCommandHandler : IValueCommandHandler<PlainValueCommand, int>
    {
        public ValueTask<int> HandleAsync(
            PlainValueCommand request,
            CancellationToken cancellationToken)
            => new(0);
    }

    private sealed class PlainValueQueryHandler : IValueQueryHandler<PlainValueQuery, int>
    {
        public ValueTask<int> HandleAsync(
            PlainValueQuery request,
            CancellationToken cancellationToken)
            => new(0);
    }

    #endregion
}
