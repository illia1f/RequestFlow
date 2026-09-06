using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class UnusedStageRuleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_A_Filter_Excluding_Every_Handler_When_Freezing_Then_The_Message_Names_Handler_Filters(
        bool inspect)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o
            .AddHandler<FilteredRequestHandler>()
            .AddStage<FilteredStage>(s => s.WhereHandlerImplements<IExcludedHandler>())
            .DisallowUnusedStages());
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
        {
            if (inspect)
                provider.InspectRequestFlow<FilteredRequest>();
            else
                provider.ValidateRequestFlow();
        });

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0105");
        problem.Message.ShouldContain("handler filter");
        problem.Message.ShouldContain("WhereHandlerImplements");
    }

    [Fact]
    public void Given_Stage_Reaching_No_Request_When_Validating_Then_Reports_The_Stage()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(int))
            .AddStageDeclaration(typeof(string))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0105");
        problem.Subject.ShouldBe(typeof(string));
        problem.Message.ShouldContain("applies to no registered request");
    }

    [Fact]
    public void Given_Stage_In_A_Chain_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), r => r.AddStage(typeof(string), typeof(string)))
            .AddStageDeclaration(typeof(string))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_Unused_Stage_Declared_Twice_When_Validating_Then_Reports_Once()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddStageDeclaration(typeof(string))
            .AddStageDeclaration(typeof(string))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        problems.Count.ShouldBe(1);
    }

    // The application asked for the unhandled requests, so a handler is not the fix it is waiting for.
    [Fact]
    public void Given_Unhandled_Requests_Allowed_When_Validating_Then_The_Message_Names_The_Opt_In_Word_For_Word()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(int))
            .AddStageDeclaration(typeof(string))
            .BuildContext(unhandledRequestsAllowed: true);

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        problems.ShouldHaveSingleItem().Message.ShouldBe(
            BaseMessage(typeof(string))
            + " Some registered requests have no handler, which AllowUnhandledRequests permits; a stage "
            + "reaching only those still counts as unused.");
    }

    [Fact]
    public void Given_Unhandled_Requests_Not_Allowed_When_Validating_Then_The_Message_Names_The_Fix_Word_For_Word()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(int))
            .AddStageDeclaration(typeof(string))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        problems.ShouldHaveSingleItem().Message.ShouldBe(
            BaseMessage(typeof(string))
            + " Some registered requests have no handler; a stage reaching only those counts as unused, so "
            + "the missing handler may be the fix.");
    }

    // Neither flag value changes the message once every request has a handler, since the sentence
    // about them is what the flag picks between.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_All_Requests_Handled_When_Validating_Then_The_Message_Is_The_Base_One(
        bool unhandledRequestsAllowed)
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), r => r.AddHandler(typeof(object), typeof(bool)))
            .AddStageDeclaration(typeof(string))
            .BuildContext(unhandledRequestsAllowed);

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        problems.ShouldHaveSingleItem().Message.ShouldBe(BaseMessage(typeof(string)));
    }

    [Fact]
    public void Given_A_Stream_Stage_Reaching_Nothing_When_Unused_Stages_Are_Disallowed_Then_Reports_It()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<UnusedStageRuleTests>();
            o.AddStreamStage<RequestFlow.Tests.ValidationFixtures.UnreachedStreamStage>();
            o.DisallowUnusedStages();
        });

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        exception.Problems.ShouldContain(p =>
            p.Code == "RF0105"
            && p.Subject == typeof(RequestFlow.Tests.ValidationFixtures.UnreachedStreamStage));
    }

    #region Initialization

    private readonly UnusedStageRule _sut = new();

    #endregion

    #region Helpers

    public sealed record FilteredRequest : IRequest;

    public sealed class FilteredRequestHandler : IRequestHandler<FilteredRequest>
    {
        public Task HandleAsync(FilteredRequest request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private interface IExcludedHandler
    { }

    private sealed class FilteredStage : IRequestStage<FilteredRequest>
    {
        public Task HandleAsync(FilteredRequest request, Continuation next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    // The assembly name is part of the message and differs per target framework, so it is read off
    // the type rather than written out.
    private static string BaseMessage(Type stageType)
        => $"Stage '{stageType.FullName}' from assembly '{stageType.Assembly.GetName().Name}' applies to no "
            + "registered request; widen its generic constraints, check its WhereHandlerImplements handler filter, "
            + "scan the assembly holding the requests it "
            + "targets, or drop DisallowUnusedStages.";

    #endregion
}
