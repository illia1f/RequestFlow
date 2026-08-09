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

    #region Initialization

    private readonly DuplicateStageRule _sut = new();

    #endregion
}
