using Microsoft.Extensions.DependencyInjection;
using RequestFlow;
using RequestFlow.Tests.ValidationFixtures;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class ValueStageResponseMismatchRuleTests
{
    [Theory]
    [InlineData(typeof(StringValue), typeof(WideValueStage))]
    [InlineData(typeof(IValueRequest<string>), typeof(ExactValueContractStage))]
    [InlineData(typeof(DerivedValue), typeof(WideValueStage))]
    [InlineData(typeof(StringValue), typeof(OpenWideValueStage<>))]
    public void Given_A_Value_Stage_With_A_Wider_Response_When_Validating_Then_Reports_RF0130(
        Type requestType, Type stageType)
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(requestType)
            .AddStageDeclaration(stageType, typeof(IValueRequestStage<,>))
            .BuildContext();

        RequestFlowValidationProblem problem = _sut.Validate(context).ShouldHaveSingleItem();

        problem.Code.ShouldBe(ProblemCodes.ValueStageResponseMismatch);
        problem.Subject.ShouldBe(stageType);
        problem.Message.ShouldContain("System.Object");
        problem.Message.ShouldContain("System.String");
    }

    [Fact]
    public void Given_A_Closed_Value_Stage_With_The_Declared_Response_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringValue))
            .AddStageDeclaration(typeof(MatchingValueStage), typeof(IValueRequestStage<,>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_An_Open_Two_Parameter_Value_Stage_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringValue))
            .AddStageDeclaration(typeof(OpenValueStage<,>), typeof(IValueRequestStage<,>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_An_Open_One_Parameter_Value_Stage_With_The_Declared_Response_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringValue))
            .AddStageDeclaration(typeof(OpenMatchingValueStage<>), typeof(IValueRequestStage<,>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_An_Open_Value_Stage_Whose_Constraints_Exclude_The_Request_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringValue))
            .AddStageDeclaration(typeof(OpenIntValueStage<>), typeof(IValueRequestStage<,>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Plain_Void_Value_Stage_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(VoidValue))
            .AddStageDeclaration(typeof(VoidValueStage), typeof(IValueRequestStage<>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Filtered_Wide_Value_Stage_Whose_Filter_Excludes_The_Handler_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringValue), request => request.AddHandler(
                typeof(UnmarkedValueHandler),
                typeof(string),
                typeof(IValueRequestHandler<,>)))
            .AddStageDeclaration(typeof(OpenWideValueStage<>), typeof(IValueRequestStage<,>))
            .BuildContext();
        StageDeclarationFacts facts = Facts(typeof(IValueHandlerMarker));

        new ValueStageResponseMismatchRule(facts).Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Filtered_Wide_Value_Stage_Whose_Filter_Admits_The_Handler_When_Validating_Then_Reports_RF0130()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringValue), request => request.AddHandler(
                typeof(MarkedValueHandler),
                typeof(string),
                typeof(IValueRequestHandler<,>)))
            .AddStageDeclaration(typeof(OpenWideValueStage<>), typeof(IValueRequestStage<,>))
            .BuildContext();
        StageDeclarationFacts facts = Facts(typeof(IValueHandlerMarker));

        RequestFlowValidationProblem problem = new ValueStageResponseMismatchRule(facts)
            .Validate(context)
            .ShouldHaveSingleItem();

        problem.Code.ShouldBe(ProblemCodes.ValueStageResponseMismatch);
        problem.Subject.ShouldBe(typeof(OpenWideValueStage<>));
    }

    [Fact]
    public void Given_A_Value_Request_With_Two_Response_Contracts_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(TwoValueContracts))
            .AddStageDeclaration(typeof(TwoValueContractsStage), typeof(IValueRequestStage<,>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Task_And_Value_Request_With_A_Wide_Value_Stage_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(TaskAndValue))
            .AddStageDeclaration(typeof(TaskAndValueStage), typeof(IValueRequestStage<,>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Task_Stage_Declaration_When_Validating_Value_Stages_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(TaskValue))
            .AddStageDeclaration(typeof(TaskStage))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Wide_Value_Stage_Wrapping_Another_Request_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(ObjectValue), request => request.AddStage(
                typeof(OpenWideValueStage<>),
                typeof(OpenWideValueStage<ObjectValue>),
                typeof(IValueRequestStage<,>)))
            .AddRequest(typeof(StringValue))
            .AddStageDeclaration(typeof(OpenWideValueStage<>), typeof(IValueRequestStage<,>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Wide_Value_Stage_Fixture_When_Freezing_Then_RF0130_Is_Aggregated()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options =>
        {
            options.RegisterHandlersFromAssembly(typeof(StageValue).Assembly);
            options.ExcludeHandler<WideValueHandler>();
            options.AddValueStage(typeof(RequestFlow.Tests.ValidationFixtures.WideValueStage<>));
        });
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.GetRequiredService<IValueRequestDispatcher>());

        exception.Problems.ShouldContain(problem =>
            problem.Code == ProblemCodes.ValueStageResponseMismatch
            && problem.Subject == typeof(RequestFlow.Tests.ValidationFixtures.WideValueStage<>));
    }

    [Fact]
    public void Given_A_Filtered_And_Unfiltered_Duplicate_Wide_Value_Stage_When_Freezing_Then_Reports_RF0103_And_RF0130()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options => options
            .AddHandler<DuplicateValueHandler>()
            .AddValueStage<DuplicateWideValueStage>(
                stage => stage.WhereHandlerImplements<IValueHandlerMarker>())
            .AddValueStage<DuplicateWideValueStage>());
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.GetRequiredService<IValueRequestDispatcher>());

        exception.Problems.Count.ShouldBe(2);
        exception.Problems.ShouldContain(problem =>
            problem.Code == ProblemCodes.DuplicateStage
            && problem.Subject == typeof(DuplicateWideValueStage));
        exception.Problems.ShouldContain(problem =>
            problem.Code == ProblemCodes.ValueStageResponseMismatch
            && problem.Subject == typeof(DuplicateWideValueStage));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_A_Dual_Family_Stage_With_A_Value_Response_Mismatch_When_Freezing_In_Either_Order_Then_Reports_RF0103_And_RF0130(
        bool valueFirst)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options =>
        {
            options
                .AddHandler<DualTaskHandler>()
                .AddHandler<DualValueHandler>();

            if (valueFirst)
            {
                options
                    .AddValueStage<DualFamilyStage>()
                    .AddStage<DualFamilyStage>();
            }
            else
            {
                options
                    .AddStage<DualFamilyStage>()
                    .AddValueStage<DualFamilyStage>();
            }
        });
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.GetRequiredService<IValueRequestDispatcher>());

        exception.Problems.Count.ShouldBe(2);
        exception.Problems.ShouldContain(problem =>
            problem.Code == ProblemCodes.DuplicateStage
            && problem.Subject == typeof(DualFamilyStage));
        exception.Problems.ShouldContain(problem =>
            problem.Code == ProblemCodes.ValueStageResponseMismatch
            && problem.Subject == typeof(DualFamilyStage));
    }

    #region Initialization

    private readonly ValueStageResponseMismatchRule _sut = new();

    #endregion

    #region Helpers

    private static StageDeclarationFacts Facts(Type handlerFilter)
        => new([
            new StageDeclaration(
                typeof(OpenWideValueStage<>),
                handlerFilter,
                StageFamily.Value),
        ]);

    private abstract record StringValue : IValueRequest<string>;

    private abstract record DerivedValue : StringValue;

    private abstract record ObjectValue : IValueRequest<object>;

    private abstract record VoidValue : IValueRequest;

    private abstract record TwoValueContracts : IValueRequest<string>, IValueRequest<int>;

    private abstract record TaskAndValue : IRequest<int>, IValueRequest<string>;

    private abstract record TaskValue : IRequest<int>;

    private abstract class WideValueStage : IValueRequestStage<StringValue, object>
    {
        public abstract ValueTask<object> HandleAsync(
            StringValue request,
            ValueContinuation<object> next,
            CancellationToken cancellationToken);
    }

    private abstract class ExactValueContractStage
        : IValueRequestStage<IValueRequest<string>, object>
    {
        public abstract ValueTask<object> HandleAsync(
            IValueRequest<string> request,
            ValueContinuation<object> next,
            CancellationToken cancellationToken);
    }

    private abstract class MatchingValueStage : IValueRequestStage<StringValue, string>
    {
        public abstract ValueTask<string> HandleAsync(
            StringValue request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken);
    }

    private abstract class OpenValueStage<TRequest, TResponse>
        : IValueRequestStage<TRequest, TResponse>
        where TRequest : IValueRequest<TResponse>
    {
        public abstract ValueTask<TResponse> HandleAsync(
            TRequest request,
            ValueContinuation<TResponse> next,
            CancellationToken cancellationToken);
    }

    private abstract class OpenWideValueStage<TRequest>
        : IValueRequestStage<TRequest, object>
        where TRequest : IValueRequest<object>
    {
        public abstract ValueTask<object> HandleAsync(
            TRequest request,
            ValueContinuation<object> next,
            CancellationToken cancellationToken);
    }

    private abstract class OpenMatchingValueStage<TRequest>
        : IValueRequestStage<TRequest, string>
        where TRequest : IValueRequest<string>
    {
        public abstract ValueTask<string> HandleAsync(
            TRequest request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken);
    }

    private abstract class OpenIntValueStage<TRequest>
        : IValueRequestStage<TRequest, int>
        where TRequest : IValueRequest<int>
    {
        public abstract ValueTask<int> HandleAsync(
            TRequest request,
            ValueContinuation<int> next,
            CancellationToken cancellationToken);
    }

    private abstract class VoidValueStage : IValueRequestStage<VoidValue>
    {
        public abstract ValueTask HandleAsync(
            VoidValue request,
            ValueContinuation next,
            CancellationToken cancellationToken);
    }

    private interface IValueHandlerMarker
    { }

    private abstract class UnmarkedValueHandler : IValueRequestHandler<StringValue, string>
    {
        public abstract ValueTask<string> HandleAsync(
            StringValue request,
            CancellationToken cancellationToken);
    }

    private abstract class MarkedValueHandler
        : IValueRequestHandler<StringValue, string>, IValueHandlerMarker
    {
        public abstract ValueTask<string> HandleAsync(
            StringValue request,
            CancellationToken cancellationToken);
    }

    private sealed record DuplicateValue : IValueRequest<string>;

    private sealed class DuplicateValueHandler
        : IValueRequestHandler<DuplicateValue, string>
    {
        public ValueTask<string> HandleAsync(
            DuplicateValue request,
            CancellationToken cancellationToken)
            => new(string.Empty);
    }

    private sealed class DuplicateWideValueStage
        : IValueRequestStage<DuplicateValue, object>
    {
        public ValueTask<object> HandleAsync(
            DuplicateValue request,
            ValueContinuation<object> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed record DualTask : IRequest<string>;

    private sealed record DualValue : IValueRequest<string>;

    private sealed class DualTaskHandler : IRequestHandler<DualTask, string>
    {
        public Task<string> HandleAsync(
            DualTask request,
            CancellationToken cancellationToken)
            => Task.FromResult(string.Empty);
    }

    private sealed class DualValueHandler : IValueRequestHandler<DualValue, string>
    {
        public ValueTask<string> HandleAsync(
            DualValue request,
            CancellationToken cancellationToken)
            => new(string.Empty);
    }

    private sealed class DualFamilyStage
        : IRequestStage<DualTask, string>, IValueRequestStage<DualValue, object>
    {
        public Task<string> HandleAsync(
            DualTask request,
            Continuation<string> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);

        public ValueTask<object> HandleAsync(
            DualValue request,
            ValueContinuation<object> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private abstract class TwoValueContractsStage
        : IValueRequestStage<TwoValueContracts, string>
    {
        public abstract ValueTask<string> HandleAsync(
            TwoValueContracts request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken);
    }

    private abstract class TaskAndValueStage : IValueRequestStage<TaskAndValue, object>
    {
        public abstract ValueTask<object> HandleAsync(
            TaskAndValue request,
            ValueContinuation<object> next,
            CancellationToken cancellationToken);
    }

    private abstract class TaskStage : IRequestStage<TaskValue, int>
    {
        public abstract Task<int> HandleAsync(
            TaskValue request,
            Continuation<int> next,
            CancellationToken cancellationToken);
    }

    #endregion
}
