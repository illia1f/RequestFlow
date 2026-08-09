using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class UnhandledRequestRuleTests
{
    [Fact]
    public void Given_Request_Without_Handler_When_Validating_Then_Reports_The_Request()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(int))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0102");
        problem.Subject.ShouldBe(typeof(int));
        problem.Message.ShouldContain("has no handler");
    }

    [Fact]
    public void Given_Handled_Request_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), r => r.AddHandler(typeof(string), typeof(bool)))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    #region Initialization

    private readonly UnhandledRequestRule _sut = new();

    #endregion
}
