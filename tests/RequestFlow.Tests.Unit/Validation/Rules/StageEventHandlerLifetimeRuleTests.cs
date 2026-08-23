using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class StageEventHandlerLifetimeRuleTests
{
    [Fact]
    public void Given_A_Singleton_Stage_That_Also_Handles_Events_When_Validating_Then_Reports_The_Class()
    {
        RequestFlowValidationContext context = Context(RequestFlowLifetime.Singleton);

        RequestFlowValidationProblem problem = _sut.Validate(context).ShouldHaveSingleItem();

        problem.Code.ShouldBe("RF0118");
        problem.Subject.ShouldBe(typeof(DualRoleStage));
        problem.Message.ShouldContain(typeof(DualRoleStage).FullName!);
        problem.Message.ShouldContain("Singleton");
        problem.Message.ShouldContain("Transient");
    }

    [Fact]
    public void Given_One_Class_In_Two_Stage_Declarations_When_Validating_Then_Reports_Once()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(DualPing), request => request
                .AddStage(typeof(DualRoleStage), typeof(DualRoleStage)))
            .AddStageDeclaration(typeof(DualRoleStage), RequestFlowLifetime.Scoped)
            .AddStageDeclaration(typeof(DualRoleStage), RequestFlowLifetime.Scoped)
            .AddEvent(typeof(DualEvent))
            .AddEventHandler(typeof(DualRoleStage), typeof(DualEvent))
            .BuildContext();

        _sut.Validate(context).ShouldHaveSingleItem().Code.ShouldBe("RF0118");
    }

    [Fact]
    public void Given_A_Stage_And_Its_Event_Handler_On_One_Lifetime_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = Context(RequestFlowLifetime.Transient);

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Stage_That_Reached_No_Request_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddStageDeclaration(typeof(DualRoleStage), RequestFlowLifetime.Singleton)
            .AddEvent(typeof(DualEvent))
            .AddEventHandler(typeof(DualRoleStage), typeof(DualEvent))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Stage_That_Handles_No_Event_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(DualPing), request => request
                .AddStage(typeof(DualRoleStage), typeof(DualRoleStage)))
            .AddStageDeclaration(typeof(DualRoleStage), RequestFlowLifetime.Singleton)
            .AddEvent(typeof(DualEvent))
            .AddEventHandler(typeof(PlainEventHandler), typeof(DualEvent))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    #region Helpers

    private readonly StageEventHandlerLifetimeRule _sut = new();

    private static RequestFlowValidationContext Context(RequestFlowLifetime stageLifetime)
        => new RequestFlowModelBuilder()
            .AddRequest(typeof(DualPing), request => request
                .AddStage(typeof(DualRoleStage), typeof(DualRoleStage)))
            .AddStageDeclaration(typeof(DualRoleStage), stageLifetime)
            .AddEvent(typeof(DualEvent))
            .AddEventHandler(typeof(DualRoleStage), typeof(DualEvent))
            .BuildContext();

    // Plain types: the model builder takes any type, and a scanned IRequest with no handler
    // would fail every other test that scans this assembly.
    private sealed record DualPing;

    private sealed record DualEvent : IEvent;

    private sealed class DualRoleStage
    { }

    private sealed class PlainEventHandler
    { }

    #endregion
}
