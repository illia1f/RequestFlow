using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class ProblemCodesTests
{
    [Fact]
    public void Given_The_Problem_Codes_Class_When_Reflecting_Then_It_Is_Public()
    {
        typeof(ProblemCodes).IsPublic.ShouldBeTrue();
    }

    [Fact]
    public void Given_The_Problem_Codes_Class_When_Reflecting_Then_It_Ships_With_The_Problem_Type()
    {
        typeof(ProblemCodes).Assembly.ShouldBe(typeof(RequestFlowValidationProblem).Assembly);
    }

    [Theory]
    [InlineData(ProblemCodes.MultiContractStreamRequest, "RF0108")]
    [InlineData(ProblemCodes.RequestAndStreamRequest, "RF0109")]
    [InlineData(ProblemCodes.StreamItemMismatch, "RF0110")]
    [InlineData(ProblemCodes.StreamStageItemMismatch, "RF0111")]
    [InlineData(ProblemCodes.HandlerResponseMismatch, "RF0112")]
    [InlineData(ProblemCodes.StageResponseMismatch, "RF0113")]
    [InlineData(ProblemCodes.StageEventHandlerLifetime, "RF0118")]
    [InlineData(ProblemCodes.EventHandlerIsInterface, "RF0015")]
    [InlineData(ProblemCodes.EventHandlerAbstract, "RF0016")]
    [InlineData(ProblemCodes.EventHandlerMissingContract, "RF0017")]
    [InlineData(ProblemCodes.ManualHandlerIsInterface, "RF0018")]
    [InlineData(ProblemCodes.ManualHandlerAbstract, "RF0019")]
    [InlineData(ProblemCodes.ManualHandlerMissingContract, "RF0020")]
    [InlineData(ProblemCodes.EventStrategyIsInterface, "RF0013")]
    [InlineData(ProblemCodes.EventStrategyAbstract, "RF0014")]
    [InlineData(ProblemCodes.ConflictingEventStrategies, "RF0119")]
    [InlineData(ProblemCodes.AmbiguousEventStrategy, "RF0120")]
    [InlineData(ProblemCodes.EventStrategyLifetime, "RF0121")]
    [InlineData(ProblemCodes.UnusedEventStrategy, "RF0122")]
    [InlineData(ProblemCodes.EventStrategyRoleLifetime, "RF0123")]
    [InlineData(ProblemCodes.NonConcreteRequest, "RF0124")]
    [InlineData(ProblemCodes.MultiContractValueRequest, "RF0125")]
    [InlineData(ProblemCodes.RequestAndValueRequest, "RF0126")]
    [InlineData(ProblemCodes.StreamRequestAndValueRequest, "RF0127")]
    [InlineData(ProblemCodes.ValueRequestAndEvent, "RF0128")]
    [InlineData(ProblemCodes.ValueHandlerResponseMismatch, "RF0129")]
    [InlineData(ProblemCodes.ValueStageResponseMismatch, "RF0130")]
    public void Given_A_Problem_Code_When_Reading_Then_It_Matches_The_Documented_Value(
        string code, string documentedValue)
    {
        code.ShouldBe(documentedValue);
    }
}
