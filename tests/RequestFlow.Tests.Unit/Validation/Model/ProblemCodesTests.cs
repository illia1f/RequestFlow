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

    [Fact]
    public void Given_The_Stage_Event_Handler_Lifetime_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.StageEventHandlerLifetime.ShouldBe("RF0118");
    }

    [Fact]
    public void Given_The_Event_Handler_Interface_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.EventHandlerIsInterface.ShouldBe("RF0015");
    }

    [Fact]
    public void Given_The_Event_Handler_Abstract_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.EventHandlerAbstract.ShouldBe("RF0016");
    }

    [Fact]
    public void Given_The_Event_Handler_Missing_Contract_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.EventHandlerMissingContract.ShouldBe("RF0017");
    }

    [Fact]
    public void Given_The_Manual_Handler_Interface_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.ManualHandlerIsInterface.ShouldBe("RF0018");
    }

    [Fact]
    public void Given_The_Manual_Handler_Abstract_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.ManualHandlerAbstract.ShouldBe("RF0019");
    }

    [Fact]
    public void Given_The_Manual_Handler_Missing_Contract_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.ManualHandlerMissingContract.ShouldBe("RF0020");
    }

    [Fact]
    public void Given_The_Event_Strategy_Interface_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.EventStrategyIsInterface.ShouldBe("RF0013");
    }

    [Fact]
    public void Given_The_Event_Strategy_Abstract_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.EventStrategyAbstract.ShouldBe("RF0014");
    }

    [Fact]
    public void Given_The_Conflicting_Event_Strategies_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.ConflictingEventStrategies.ShouldBe("RF0119");
    }

    [Fact]
    public void Given_The_Ambiguous_Event_Strategy_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.AmbiguousEventStrategy.ShouldBe("RF0120");
    }

    [Fact]
    public void Given_The_Event_Strategy_Lifetime_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.EventStrategyLifetime.ShouldBe("RF0121");
    }

    [Fact]
    public void Given_The_Unused_Event_Strategy_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.UnusedEventStrategy.ShouldBe("RF0122");
    }

    [Fact]
    public void Given_The_Event_Strategy_Role_Lifetime_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.EventStrategyRoleLifetime.ShouldBe("RF0123");
    }

    [Fact]
    public void Given_The_Non_Concrete_Request_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.NonConcreteRequest.ShouldBe("RF0124");
    }

    [Fact]
    public void Given_The_Multi_Contract_Value_Request_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.MultiContractValueRequest.ShouldBe("RF0125");
    }

    [Fact]
    public void Given_The_Request_And_Value_Request_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.RequestAndValueRequest.ShouldBe("RF0126");
    }

    [Fact]
    public void Given_The_Stream_Request_And_Value_Request_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.StreamRequestAndValueRequest.ShouldBe("RF0127");
    }

    [Fact]
    public void Given_The_Value_Request_And_Event_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.ValueRequestAndEvent.ShouldBe("RF0128");
    }

    [Fact]
    public void Given_The_Value_Handler_Response_Mismatch_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.ValueHandlerResponseMismatch.ShouldBe("RF0129");
    }

    [Fact]
    public void Given_The_Value_Stage_Response_Mismatch_Code_When_Reading_Then_It_Matches_The_Documented_Value()
    {
        ProblemCodes.ValueStageResponseMismatch.ShouldBe("RF0130");
    }
}
