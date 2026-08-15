using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class RequestFlowValidationProblemTests
{
    [Fact]
    public void Given_Same_Code_Message_And_Subject_When_Comparing_Problems_Then_They_Are_Equal()
    {
        var first = new RequestFlowValidationProblem("RF0101", "duplicate", typeof(string));
        var second = new RequestFlowValidationProblem("RF0101", "duplicate", typeof(string));

        first.Equals(second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Given_Different_Code_When_Comparing_Problems_Then_They_Are_Not_Equal()
    {
        var first = new RequestFlowValidationProblem("RF0101", "duplicate");
        var second = new RequestFlowValidationProblem("RF0102", "duplicate");

        first.Equals(second).ShouldBeFalse();
    }

    [Fact]
    public void Given_Different_Subject_When_Comparing_Problems_Then_They_Are_Not_Equal()
    {
        var first = new RequestFlowValidationProblem("RF0101", "duplicate", typeof(string));
        var second = new RequestFlowValidationProblem("RF0101", "duplicate", typeof(int));

        first.Equals(second).ShouldBeFalse();
    }

    [Fact]
    public void Given_Same_Code_Message_And_Subject_When_Comparing_With_Operator_Then_They_Are_Equal()
    {
        var first = new RequestFlowValidationProblem("RF0101", "duplicate", typeof(string));
        var second = new RequestFlowValidationProblem("RF0101", "duplicate", typeof(string));

        (first == second).ShouldBeTrue();
        (first != second).ShouldBeFalse();
    }

    [Fact]
    public void Given_Different_Code_When_Comparing_With_Operator_Then_They_Are_Not_Equal()
    {
        var first = new RequestFlowValidationProblem("RF0101", "duplicate");
        var second = new RequestFlowValidationProblem("RF0102", "duplicate");

        (first == second).ShouldBeFalse();
        (first != second).ShouldBeTrue();
    }

    [Fact]
    public void Given_Null_Code_When_Creating_Problem_Then_Throws_Argument_Null_Exception()
    {
        Should.Throw<ArgumentNullException>(() => new RequestFlowValidationProblem(null!, "message"));
    }

    [Fact]
    public void Given_Null_Message_When_Creating_Problem_Then_Throws_Argument_Null_Exception()
    {
        Should.Throw<ArgumentNullException>(() => new RequestFlowValidationProblem("RF0101", null!));
    }

    [Fact]
    public void Given_Code_And_Message_When_Formatting_Then_Returns_Code_Colon_Message()
    {
        var problem = new RequestFlowValidationProblem("RF0101", "duplicate handler");

        problem.ToString().ShouldBe("RF0101: duplicate handler");
    }
}
