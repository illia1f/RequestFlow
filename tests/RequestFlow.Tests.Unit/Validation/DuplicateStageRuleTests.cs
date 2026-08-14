using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class DuplicateStageRuleTests
{
    [Fact]
    public void Given_Same_Stage_Declared_Twice_When_Validating_Then_Reports_One_Problem()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddStageDeclaration(typeof(string))
            .AddStageDeclaration(typeof(string))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0103");
        problem.Subject.ShouldBe(typeof(string));
        problem.Message.ShouldContain("registered more than once");
        problem.Message.ShouldContain("whatever each call filtered on");
    }

    [Fact]
    public void Given_Same_Stage_Declared_Three_Times_When_Validating_Then_Reports_One_Problem()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddStageDeclaration(typeof(string))
            .AddStageDeclaration(typeof(string))
            .AddStageDeclaration(typeof(string))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        problems.ShouldHaveSingleItem().Code.ShouldBe("RF0103");
    }

    [Fact]
    public void Given_Distinct_Stages_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddStageDeclaration(typeof(string))
            .AddStageDeclaration(typeof(int))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Stage_On_Both_Families_Declared_Twice_With_AddStage_When_Resolving_Then_The_Message_Names_AddStage()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<DuplicateStageRuleTests>();
            o.AddStage<DualStage>();
            o.AddStage<DualStage>();
        });

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0103");
        problem.Message.ShouldContain("Remove the duplicate AddStage call");
    }

    [Fact]
    public void Given_A_Stage_Registered_By_Both_Families_When_Resolving_Then_The_Message_Names_Both_Calls()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<DuplicateStageRuleTests>();
            o.AddStreamStage<DualStage>();
            o.AddStage<DualStage>();
        });

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0103");
        problem.Message.ShouldContain("Remove the AddStage call or the AddStreamStage call");
    }

    #region Initialization

    private readonly DuplicateStageRule _sut = new();

    #endregion

    #region Helpers

    // Abstract keeps it out of the scanner, so the pair of contracts is never registered as a
    // request of its own.
    private abstract record Dual : IRequest<string>, IStreamRequest<string>;

    // The shape a package builds on top of both core contracts at once.
    private interface IDualStage<TRequest, TResponse>
        : IRequestStage<TRequest, TResponse>, IStreamRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>, IStreamRequest<TResponse>
    { }

    private sealed class DualStage : IDualStage<Dual, string>
    {
        public Task<string> HandleAsync(Dual request, Continuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);

        public IAsyncEnumerable<string> Handle(
            Dual request, StreamContinuation<string> next, CancellationToken cancellationToken)
            => next.Invoke(cancellationToken);
    }

    #endregion
}
