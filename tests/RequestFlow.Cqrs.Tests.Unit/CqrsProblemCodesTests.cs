namespace RequestFlow.Cqrs.Tests.Unit;

public sealed class CqrsProblemCodesTests
{
    // Same contract as RequestFlow's ProblemCodes: a caller matches the constant, not the
    // literal, so the class has to be public.
    [Fact]
    public void Given_The_Cqrs_Problem_Codes_Class_When_Reflecting_Then_It_Is_Public()
    {
        typeof(CqrsProblemCodes).IsPublic.ShouldBeTrue();
    }

    // Also like ProblemCodes: the constant lives in the abstractions package, so a caller that
    // never references the runtime one can still match on it.
    [Fact]
    public void Given_The_Cqrs_Problem_Codes_Class_When_Reflecting_Then_It_Ships_In_The_Abstractions_Package()
    {
        typeof(CqrsProblemCodes).Assembly.ShouldBe(typeof(ICommand<>).Assembly);
    }

    [Fact]
    public void Given_The_Split_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        CqrsProblemCodes.CommandQuerySplit.ShouldBe("CQRS0001");
    }
}
