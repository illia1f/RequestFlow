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

    [Fact]
    public void Given_The_Multi_Contract_Stream_Request_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.MultiContractStreamRequest.ShouldBe("RF0108");
    }

    [Fact]
    public void Given_The_Request_And_Stream_Request_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.RequestAndStreamRequest.ShouldBe("RF0109");
    }

    [Fact]
    public void Given_The_Stream_Item_Mismatch_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.StreamItemMismatch.ShouldBe("RF0110");
    }

    [Fact]
    public void Given_The_Stream_Stage_Item_Mismatch_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.StreamStageItemMismatch.ShouldBe("RF0111");
    }

    [Fact]
    public void Given_The_Handler_Response_Mismatch_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.HandlerResponseMismatch.ShouldBe("RF0112");
    }

    [Fact]
    public void Given_The_Stage_Response_Mismatch_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.StageResponseMismatch.ShouldBe("RF0113");
    }
}
