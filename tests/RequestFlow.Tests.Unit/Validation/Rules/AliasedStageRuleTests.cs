using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class AliasedStageRuleTests
{
    [Fact]
    public void Given_Two_Declarations_Closing_To_One_Type_When_Validating_Then_Reports_Both_Resolve()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), r => r
                .AddStage(typeof(OpenStage<>), typeof(OpenStage<int>))
                .AddStage(typeof(OpenStage<int>), typeof(OpenStage<int>)))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0104");
        problem.Message.ShouldContain("both resolve to");
        problem.Subject.ShouldBe(typeof(OpenStage<>));
    }

    // The pair is reported once, so a subject taken from the request would depend on which
    // request the scan reached first.
    [Fact]
    public void Given_The_Same_Pair_On_Two_Requests_When_Validating_Then_The_Subject_Does_Not_Depend_On_Scan_Order()
    {
        Action<RequestModelBuilder> chain = r => r
            .AddStage(typeof(OpenStage<>), typeof(OpenStage<int>))
            .AddStage(typeof(OpenStage<int>), typeof(OpenStage<int>));

        RequestFlowValidationContext first = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), chain)
            .AddRequest(typeof(long), chain)
            .BuildContext();

        RequestFlowValidationContext reversed = new RequestFlowModelBuilder()
            .AddRequest(typeof(long), chain)
            .AddRequest(typeof(int), chain)
            .BuildContext();

        Type? subject = new AliasedStageRule().Validate(first).Single().Subject;
        Type? reversedSubject = new AliasedStageRule().Validate(reversed).Single().Subject;

        subject.ShouldBe(typeof(OpenStage<>));
        reversedSubject.ShouldBe(subject);
    }

    [Fact]
    public void Given_Two_Closings_Of_One_Class_When_Validating_Then_Reports_Same_Stage_Class()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), r => r
                .AddStage(typeof(OpenStage<int>), typeof(OpenStage<int>))
                .AddStage(typeof(OpenStage<long>), typeof(OpenStage<long>)))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        problems.ShouldHaveSingleItem().Message.ShouldContain("same stage class");
    }

    [Fact]
    public void Given_The_Same_Pair_On_Two_Requests_When_Validating_Then_Reports_Once()
    {
        Action<RequestModelBuilder> chain = r => r
            .AddStage(typeof(OpenStage<>), typeof(OpenStage<int>))
            .AddStage(typeof(OpenStage<int>), typeof(OpenStage<int>));

        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), chain)
            .AddRequest(typeof(long), chain)
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        problems.Count.ShouldBe(1);
    }

    [Fact]
    public void Given_Three_Declarations_Of_One_Class_When_Validating_Then_One_Problem_Names_All_Three()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), r => r
                .AddStage(typeof(OpenStage<>), typeof(OpenStage<int>))
                .AddStage(typeof(OpenStage<int>), typeof(OpenStage<int>))
                .AddStage(typeof(OpenStage<long>), typeof(OpenStage<long>)))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0104");
        problem.Message.ShouldContain(typeof(OpenStage<>).FullName!);
        problem.Message.ShouldContain(typeof(OpenStage<int>).FullName!);
        problem.Message.ShouldContain(typeof(OpenStage<long>).FullName!);
        problem.Message.ShouldContain("3 times");
    }

    [Fact]
    public void Given_A_Verbatim_Duplicate_Beside_An_Alias_When_Validating_Then_Message_Counts_Every_Chain_Occurrence()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), r => r
                .AddStage(typeof(OpenStage<>), typeof(OpenStage<int>))
                .AddStage(typeof(OpenStage<>), typeof(OpenStage<int>))
                .AddStage(typeof(OpenStage<int>), typeof(OpenStage<int>)))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Message.ShouldContain("3 times");
        problem.Message.ShouldNotContain("one of the two");
    }

    [Fact]
    public void Given_A_Request_With_Two_Handlers_When_Validating_Then_The_Message_Drops_The_Run_Count()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), r => r
                .AddHandler(typeof(OtherStage), typeof(int))
                .AddHandler(typeof(OpenStage<long>), typeof(int))
                .AddStage(typeof(OpenStage<>), typeof(OpenStage<int>))
                .AddStage(typeof(OpenStage<>), typeof(OpenStage<long>))
                .AddStage(typeof(OpenStage<int>), typeof(OpenStage<int>)))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0104");
        problem.Message.ShouldContain("more than once");
        problem.Message.ShouldNotContain("3 times");
    }

    // Each closing can belong to a different handler's chain, and the model merges the chains
    // into one list, so nothing here says the class runs twice anywhere.
    [Fact]
    public void Given_Two_Closings_Of_One_Class_On_A_Request_With_Two_Handlers_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), r => r
                .AddHandler(typeof(FirstHandler), typeof(int))
                .AddHandler(typeof(SecondHandler), typeof(long))
                .AddStage(typeof(OpenStage<int>), typeof(OpenStage<int>))
                .AddStage(typeof(OpenStage<long>), typeof(OpenStage<long>)))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_Distinct_Stage_Classes_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), r => r
                .AddStage(typeof(OpenStage<>), typeof(OpenStage<int>))
                .AddStage(typeof(OtherStage), typeof(OtherStage)))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_Plain_Void_Value_Stage_Alias_When_Validating_Then_Message_Names_Add_Value_Stage()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(ValueVoidRequest), request => request
                .AddStage(
                    typeof(ValueVoidStage<>),
                    typeof(ValueVoidStage<ValueVoidRequest>),
                    typeof(IValueRequestStage<>))
                .AddStage(
                    typeof(ValueVoidStage<ValueVoidRequest>),
                    typeof(ValueVoidStage<ValueVoidRequest>),
                    typeof(IValueRequestStage<>)))
            .BuildContext();

        RequestFlowValidationProblem problem = _sut.Validate(context).ShouldHaveSingleItem();

        string advice = problem.Message.Substring(
            problem.Message.LastIndexOf("; ", StringComparison.Ordinal) + 2);
        advice.ShouldBe("remove one of the two AddValueStage calls.");
    }

    [Fact]
    public void Given_Task_Stage_Aliases_Registered_Through_Add_Stage_When_The_Contract_Also_Derives_From_A_Value_Stage_Then_Message_Names_Add_Stage()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options => options
            .AddHandler<MultiFamilyAliasHandler>()
            .AddStage(typeof(MultiFamilyAliasStage<,>))
            .AddStage<MultiFamilyAliasStage<MultiFamilyAliasRequest, string>>());
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.ValidateRequestFlow());

        RequestFlowValidationProblem problem = exception.Problems
            .Single(candidate => candidate.Code == ProblemCodes.AliasedStage);
        string advice = problem.Message.Substring(
            problem.Message.LastIndexOf("; ", StringComparison.Ordinal) + 2);
        advice.ShouldBe("remove one of the two AddStage calls.");
    }

    #region Initialization

    private readonly AliasedStageRule _sut = new();

    #endregion

    #region Helpers

    private sealed class OpenStage<T>
    { }

    private sealed class OtherStage
    { }

    private sealed class FirstHandler
    { }

    private sealed class SecondHandler
    { }

    private abstract record ValueVoidRequest : IValueRequest;

    private sealed class ValueVoidStage<TRequest> : IValueRequestStage<TRequest>
        where TRequest : IValueRequest
    {
        public ValueTask HandleAsync(
            TRequest request,
            ValueContinuation next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed record MultiFamilyAliasRequest : IRequest<string>;

    private sealed class MultiFamilyAliasHandler
        : IRequestHandler<MultiFamilyAliasRequest, string>
    {
        public Task<string> HandleAsync(
            MultiFamilyAliasRequest request,
            CancellationToken cancellationToken)
            => Task.FromResult(string.Empty);
    }

    private interface IMultiFamilyStage<in TRequest, TResponse>
        : IRequestStage<TRequest, TResponse>,
          IValueRequestStage<IValueRequest<string>, object>
        where TRequest : IRequest<TResponse>
    { }

    private sealed class MultiFamilyAliasStage<TRequest, TResponse>
        : IMultiFamilyStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request,
            Continuation<TResponse> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);

        public ValueTask<object> HandleAsync(
            IValueRequest<string> request,
            ValueContinuation<object> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    #endregion
}
