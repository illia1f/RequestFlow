using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class StreamRequestContractRuleTests
{
    [Fact]
    public void Given_A_Request_With_Two_Stream_Contracts_When_Validating_Then_Reports_The_Request()
    {
        RequestFlowValidationContext context = Context(typeof(TwoStreams));

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0108");
        problem.Subject.ShouldBe(typeof(TwoStreams));
        problem.Message.ShouldContain("more than one stream request contract");
        problem.Message.ShouldContain("System.String");
        problem.Message.ShouldContain("System.Int32");
    }

    [Fact]
    public void Given_A_Request_That_Is_Both_A_Request_And_A_Stream_Request_When_Validating_Then_Reports_The_Request()
    {
        RequestFlowValidationContext context = Context(typeof(BothFamilies));

        List<RequestFlowValidationProblem> problems = [.. _requestAndStreamRule.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0109");
        problem.Subject.ShouldBe(typeof(BothFamilies));
        problem.Message.ShouldContain("IRequest");
        problem.Message.ShouldContain("IStreamRequest");
    }

    [Fact]
    public void Given_A_Request_With_One_Stream_Contract_When_Validating_Then_Reports_Nothing()
    {
        _sut.Validate(Context(typeof(SingleStream))).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Plain_Request_When_Validating_Then_Reports_Nothing()
    {
        _sut.Validate(Context(typeof(PlainRequest))).ShouldBeEmpty();
    }

    [Fact]
    public void Given_Two_Marker_Interfaces_Sharing_One_Stream_Contract_When_Validating_Then_Reports_Nothing()
    {
        _sut.Validate(Context(typeof(SharedStream))).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Handler_With_A_Wider_Item_Type_When_Validating_Then_Reports_The_Handler()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringStream), r => r.AddHandler(typeof(WideItemStreamHandler), typeof(object)))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0110");
        problem.Subject.ShouldBe(typeof(WideItemStreamHandler));
        problem.Message.ShouldContain("System.Object");
        problem.Message.ShouldContain("System.String");
    }

    [Fact]
    public void Given_A_Handler_With_The_Declared_Item_Type_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(SingleStream), r => r.AddHandler(typeof(MatchingItemStreamHandler), typeof(int)))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    // RF0127 owns a type carrying stream and ValueTask contracts, so there is no sole stream item.
    [Fact]
    public void Given_A_Wide_Stream_Handler_On_A_Stream_And_Value_Request_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(
                typeof(StreamAndValue),
                request => request.AddHandler(
                    typeof(StreamAndValueHandler),
                    typeof(object),
                    typeof(IStreamRequestHandler<,>)))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Scanned_Multi_Contract_Stream_Request_When_Resolving_Dispatcher_Then_The_Problem_Is_In_The_Exception()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssembly(
            typeof(RequestFlow.Tests.ValidationFixtures.Lonely).Assembly));

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        exception.Problems.ShouldContain(p =>
            p.Code == "RF0108" && p.Subject == typeof(RequestFlow.Tests.ValidationFixtures.ForkedStream));
        exception.Problems.ShouldContain(p =>
            p.Code == "RF0109" && p.Subject == typeof(RequestFlow.Tests.ValidationFixtures.MixedFamilies));
    }

    [Fact]
    public void Given_A_Scanned_Handler_With_A_Wider_Item_Type_When_Resolving_Dispatcher_Then_The_Problem_Is_In_The_Exception()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssembly(
            typeof(RequestFlow.Tests.ValidationFixtures.Lonely).Assembly));

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        exception.Problems.ShouldContain(p =>
            p.Code == "RF0110" && p.Subject == typeof(RequestFlow.Tests.ValidationFixtures.WideStreamHandler));
    }

    #region Initialization

    private readonly StreamRequestContractRule _sut = new();

    private readonly ContractConflictRule _requestAndStreamRule = new(
        MessageContract.Request,
        MessageContract.StreamRequest,
        ProblemCodes.RequestAndStreamRequest);

    #endregion

    #region Helpers

    private static RequestFlowValidationContext Context(Type requestType)
        => new RequestFlowModelBuilder().AddRequest(requestType).BuildContext();

    // Abstract keeps these out of the scanner when other tests scan this assembly; the rule reads a
    // type's interfaces only. They also stay in this class, away from any Stream call, because two
    // IStreamRequest shapes on one type make Stream<TItem>(IStreamRequest<TItem>) ambiguous and
    // every inferred call in the same class then fails with CS0411.
    private abstract record TwoStreams : IStreamRequest<string>, IStreamRequest<int>;

    private abstract record BothFamilies : IRequest<string>, IStreamRequest<int>;

    private abstract record SingleStream : IStreamRequest<int>;

    private abstract record PlainRequest : IRequest<int>;

    private interface IFirstStreamMarker : IStreamRequest<int>
    { }

    private interface ISecondStreamMarker : IStreamRequest<int>
    { }

    private abstract record SharedStream : IFirstStreamMarker, ISecondStreamMarker;

    private abstract record StringStream : IStreamRequest<string>;

    private abstract record StreamAndValue : IStreamRequest<string>, IValueRequest<int>;

    // Compiles because IStreamRequest<TItem> is covariant: StringStream satisfies
    // IStreamRequest<object>, so the handler constraint closes over the wider item.
    private abstract class WideItemStreamHandler : IStreamRequestHandler<StringStream, object>
    {
        public abstract IAsyncEnumerable<object> Handle(StringStream request, CancellationToken cancellationToken);
    }

    private abstract class MatchingItemStreamHandler : IStreamRequestHandler<SingleStream, int>
    {
        public abstract IAsyncEnumerable<int> Handle(SingleStream request, CancellationToken cancellationToken);
    }

    private abstract class StreamAndValueHandler : IStreamRequestHandler<StreamAndValue, object>
    {
        public abstract IAsyncEnumerable<object> Handle(
            StreamAndValue request,
            CancellationToken cancellationToken);
    }

    #endregion
}
