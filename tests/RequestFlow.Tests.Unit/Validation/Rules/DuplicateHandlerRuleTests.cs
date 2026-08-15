using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class DuplicateHandlerRuleTests
{
    [Fact]
    public void Given_Two_Handlers_For_One_Request_When_Validating_Then_Reports_One_Problem()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), r => r
                .AddHandler(typeof(string), typeof(bool))
                .AddHandler(typeof(object), typeof(bool)))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0101");
        problem.Subject.ShouldBe(typeof(int));
        problem.Message.ShouldContain("more than one handler");
    }

    [Fact]
    public void Given_One_Handler_Per_Request_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), r => r.AddHandler(typeof(string), typeof(bool)))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_Unhandled_Request_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(int))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    #region Initialization

    private readonly DuplicateHandlerRule _sut = new();

    #endregion
}
