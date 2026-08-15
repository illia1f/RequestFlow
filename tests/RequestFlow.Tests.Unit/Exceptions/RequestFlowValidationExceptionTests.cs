using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class RequestFlowValidationExceptionTests
{
    [Fact]
    public void Given_Null_Problems_When_Creating_The_Exception_Then_Throws_Argument_Null_Exception()
    {
        ArgumentNullException exception =
            Should.Throw<ArgumentNullException>(() => new RequestFlowValidationException(null!));

        exception.ParamName.ShouldBe("problems");
    }

    [Fact]
    public void Given_A_Problem_When_Creating_The_Exception_Then_The_Message_Starts_With_The_Registration_Prefix()
    {
        var exception = new RequestFlowValidationException([Problem("RF0101", "duplicate handler")]);

        exception.Message.ShouldStartWith("RequestFlow registration is invalid:");
    }

    [Fact]
    public void Given_Several_Problems_When_Creating_The_Exception_Then_The_Message_Lists_Every_Problem_One_Per_Line()
    {
        var exception = new RequestFlowValidationException(
            [Problem("RF0101", "duplicate handler"), Problem("RF0102", "no handler")]);

        exception.Message.ShouldBe(
            "RequestFlow registration is invalid:" + Environment.NewLine +
            "RF0101: duplicate handler" + Environment.NewLine +
            "RF0102: no handler");
    }

    [Fact]
    public void Given_Problems_When_Creating_The_Exception_Then_They_Are_Exposed_In_The_Order_Given()
    {
        RequestFlowValidationProblem first = Problem("RF0101", "duplicate handler");
        RequestFlowValidationProblem second = Problem("RF0102", "no handler");

        var exception = new RequestFlowValidationException([first, second]);

        exception.Problems.Count.ShouldBe(2);
        exception.Problems[0].ShouldBeSameAs(first);
        exception.Problems[1].ShouldBeSameAs(second);
    }

    #region Helpers

    private static RequestFlowValidationProblem Problem(string code, string message)
        => new(code, message);

    #endregion
}
