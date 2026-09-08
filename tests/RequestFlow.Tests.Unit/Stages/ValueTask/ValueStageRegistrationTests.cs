using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class ValueStageRegistrationTests
{
    [Theory]
    [InlineData(typeof(LoggingValueStage<,>))]
    [InlineData(typeof(PingValueStage))]
    public void Given_A_Value_Stage_When_Adding_By_Type_Then_Declaration_Records_Value_Family(Type stageType)
    {
        var options = new RequestFlowOptions();

        options.AddValueStage(stageType);

        StageDeclaration declaration = options.StageDeclarations.ShouldHaveSingleItem();
        declaration.Family.ShouldBeSameAs(StageFamily.Value);
        declaration.StageType.ShouldBe(stageType);
    }

    [Fact]
    public void Given_Closed_Value_Stage_When_Adding_Generically_Then_Declaration_Records_Value_Family()
    {
        var options = new RequestFlowOptions();

        options.AddValueStage<PingValueStage>();

        StageDeclaration declaration = options.StageDeclarations.ShouldHaveSingleItem();
        declaration.Family.ShouldBeSameAs(StageFamily.Value);
        declaration.StageType.ShouldBe(typeof(PingValueStage));
    }

    [Fact]
    public void Given_Value_Stage_Filter_And_Lifetime_When_Adding_Then_Both_Are_Recorded()
    {
        var options = new RequestFlowOptions();

        options.AddValueStage(
            typeof(LoggingValueStage<,>),
            stage => stage.WhereHandlerImplements<IAudited>().AsScoped());

        StageDeclaration declaration = options.StageDeclarations.ShouldHaveSingleItem();
        declaration.HandlerFilter.ShouldBe(typeof(IAudited));
        declaration.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Given_Null_Value_Stage_Type_When_Adding_Then_Throws_Argument_Null_Exception()
    {
        var options = new RequestFlowOptions();

        Should.Throw<ArgumentNullException>(() => options.AddValueStage(null!));
    }

    [Theory]
    [InlineData(typeof(LoggingValueStage<,>))]
    [InlineData(typeof(VoidValueStage<>))]
    public void Given_An_Open_Value_Stage_When_Validating_Then_It_Is_Valid(Type stageType)
    {
        StageDeclaration[] declarations =
            [new StageDeclaration(stageType, null, StageFamily.Value)];

        Validated<StageDeclaration> result =
            RegistrationValidator.ValidateStageDeclarations(declarations);

        result.Valid.ShouldHaveSingleItem();
        result.Problems.ShouldBeEmpty();
    }

    [Fact]
    public void Given_Value_Stage_With_Misused_Parameters_When_Validating_Then_Reports_RF0012()
    {
        StageDeclaration[] declarations =
            [new StageDeclaration(typeof(MisusedValueStage<>), null, StageFamily.Value)];

        Validated<StageDeclaration> result =
            RegistrationValidator.ValidateStageDeclarations(declarations);

        RequestFlowValidationProblem problem = result.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0012");
        problem.Subject.ShouldBe(typeof(MisusedValueStage<>));
        problem.Message.ShouldContain("IValueRequestStage");
        problem.Message.ShouldContain("in that order");
    }

    [Fact]
    public void Given_Task_Stage_When_Added_As_Value_Stage_Then_Reports_RF0011()
    {
        StageDeclaration[] declarations =
            [new StageDeclaration(typeof(TaskStage), null, StageFamily.Value)];

        Validated<StageDeclaration> result =
            RegistrationValidator.ValidateStageDeclarations(declarations);

        RequestFlowValidationProblem problem = result.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0011");
        problem.Subject.ShouldBe(typeof(TaskStage));
        problem.Message.ShouldContain("IValueRequestStage<TRequest, TResponse>");
        problem.Message.ShouldContain("AddValueStage");
    }

    #region Helpers

    private interface IAudited
    { }

    private abstract record Ping : IValueRequest<string>;

    private abstract record TaskPing : IRequest<string>;

    private sealed class LoggingValueStage<TRequest, TResponse> : IValueRequestStage<TRequest, TResponse>
        where TRequest : IValueRequest<TResponse>
    {
        public ValueTask<TResponse> HandleAsync(
            TRequest request,
            ValueContinuation<TResponse> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed class PingValueStage : IValueRequestStage<Ping, string>
    {
        public ValueTask<string> HandleAsync(
            Ping request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed class VoidValueStage<TRequest> : IValueRequestStage<TRequest>
        where TRequest : IValueRequest
    {
        public ValueTask HandleAsync(
            TRequest request,
            ValueContinuation next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed class MisusedValueStage<TUnused> : IValueRequestStage<Ping, string>
    {
        public ValueTask<string> HandleAsync(
            Ping request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed class TaskStage : IRequestStage<TaskPing, string>
    {
        public Task<string> HandleAsync(
            TaskPing request,
            Continuation<string> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    #endregion
}
