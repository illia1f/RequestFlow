using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class ProblemCodesTests
{
    // docs/validation-rules.md tells a caller to match ProblemCodes constants instead of
    // literal strings, which only works while the class stays public.
    [Fact]
    public void Given_The_Problem_Codes_Class_When_Reflecting_Then_It_Is_Public()
    {
        typeof(ProblemCodes).IsPublic.ShouldBeTrue();
    }

    // An assembly referencing only the abstractions catches the exception and reads the codes,
    // so the constants ship beside the problem they describe.
    [Fact]
    public void Given_The_Problem_Codes_Class_When_Reflecting_Then_It_Ships_With_The_Problem_Type()
    {
        typeof(ProblemCodes).Assembly.ShouldBe(typeof(RequestFlowValidationProblem).Assembly);
    }
}
