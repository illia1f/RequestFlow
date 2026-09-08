using Microsoft.Extensions.DependencyInjection;
using RequestFlow;
using RequestFlow.Tests.ValidationFixtures;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class ValueRequestContractRuleTests
{
    [Fact]
    public void Given_Two_Value_Response_Contracts_When_Validating_Then_Reports_RF0125()
    {
        RequestFlowValidationContext context = Context(typeof(TwoValueContracts));

        RequestFlowValidationProblem problem = _sut.Validate(context).ShouldHaveSingleItem();

        problem.Code.ShouldBe(ProblemCodes.MultiContractValueRequest);
        problem.Subject.ShouldBe(typeof(TwoValueContracts));
        problem.Message.ShouldContain("more than one ValueTask request contract");
        problem.Message.ShouldContain("System.String");
        problem.Message.ShouldContain("System.Int32");
    }

    [Fact]
    public void Given_Two_Marker_Interfaces_Sharing_One_Value_Contract_When_Validating_Then_Reports_Nothing()
    {
        _sut.Validate(Context(typeof(SharedValueContract))).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Plain_And_Typed_Value_Request_When_Validating_Then_Reports_RF0125()
    {
        RequestFlowValidationProblem problem = _sut.Validate(
            Context(typeof(PlainAndTypedValue))).ShouldHaveSingleItem();

        problem.Code.ShouldBe(ProblemCodes.MultiContractValueRequest);
        problem.Subject.ShouldBe(typeof(PlainAndTypedValue));
    }

    [Fact]
    public void Given_A_Matching_Derived_Value_Handler_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringValue), request => request.AddHandler(
                typeof(MatchingValueHandler),
                typeof(string),
                typeof(IAuditedValueHandler<,>)))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Plain_Void_Value_Handler_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(VoidValue), request => request.AddHandler(
                typeof(VoidValueHandler),
                contractType: typeof(IValueRequestHandler<>)))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Wider_Value_Handler_When_Validating_Then_Reports_RF0129()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(WideValueRequest), request => request.AddHandler(
                typeof(WideValueRequestHandler),
                typeof(object),
                typeof(IValueRequestHandler<,>)))
            .BuildContext();

        RequestFlowValidationProblem problem = _sut.Validate(context).ShouldHaveSingleItem();

        problem.Code.ShouldBe(ProblemCodes.ValueHandlerResponseMismatch);
        problem.Subject.ShouldBe(typeof(WideValueRequestHandler));
        problem.Message.ShouldContain("System.Object");
        problem.Message.ShouldContain("System.String");
    }

    [Fact]
    public void Given_The_Exact_Value_Request_Contract_And_A_Wider_Handler_When_Validating_Then_Reports_RF0129()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(IValueRequest<string>), request => request.AddHandler(
                typeof(ExactValueContractHandler),
                typeof(object),
                typeof(IValueRequestHandler<,>)))
            .BuildContext();

        RequestFlowValidationProblem problem = _sut.Validate(context).ShouldHaveSingleItem();

        problem.Code.ShouldBe(ProblemCodes.ValueHandlerResponseMismatch);
        problem.Subject.ShouldBe(typeof(ExactValueContractHandler));
    }

    [Fact]
    public void Given_A_Base_Response_Value_Handler_When_Validating_Then_Reports_RF0129()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(FetchDogValue), request => request.AddHandler(
                typeof(BaseValueHandler),
                typeof(Animal),
                typeof(IValueRequestHandler<,>)))
            .BuildContext();

        RequestFlowValidationProblem problem = _sut.Validate(context).ShouldHaveSingleItem();

        problem.Code.ShouldBe(ProblemCodes.ValueHandlerResponseMismatch);
        problem.Subject.ShouldBe(typeof(BaseValueHandler));
    }

    [Fact]
    public void Given_Multiple_Value_Contracts_And_A_Wider_Handler_When_Validating_Then_RF0129_Is_Suppressed()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(TwoValueContracts), request => request.AddHandler(
                typeof(TwoValueContractsHandler),
                typeof(object),
                typeof(IValueRequestHandler<,>)))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        problems.ShouldHaveSingleItem().Code.ShouldBe(ProblemCodes.MultiContractValueRequest);
    }

    [Fact]
    public void Given_A_Task_And_Value_Request_With_A_Wider_Value_Handler_When_Validating_Then_RF0129_Is_Suppressed()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(TaskAndValue), request => request.AddHandler(
                typeof(TaskAndValueHandler),
                typeof(object),
                typeof(IValueRequestHandler<,>)))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Stream_And_Value_Request_With_A_Wider_Value_Handler_When_Validating_Then_RF0129_Is_Suppressed()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StreamAndValue), request => request.AddHandler(
                typeof(StreamAndValueHandler),
                typeof(object),
                typeof(IValueRequestHandler<,>)))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_The_Validation_Fixture_Assembly_When_Freezing_Then_RF0125_And_RF0129_Are_Aggregated()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options => options.RegisterHandlersFromAssembly(
            typeof(ForkedValue).Assembly));
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.GetRequiredService<IValueRequestDispatcher>());

        exception.Problems.ShouldContain(problem =>
            problem.Code == ProblemCodes.MultiContractValueRequest
            && problem.Subject == typeof(ForkedValue));
        exception.Problems.ShouldContain(problem =>
            problem.Code == ProblemCodes.ValueHandlerResponseMismatch
            && problem.Subject == typeof(WideValueHandler));
    }

    #region Initialization

    private readonly ValueRequestContractRule _sut = new();

    #endregion

    #region Helpers

    private static RequestFlowValidationContext Context(Type requestType)
        => new RequestFlowModelBuilder().AddRequest(requestType).BuildContext();

    private abstract record TwoValueContracts : IValueRequest<string>, IValueRequest<int>;

    private interface IFirstValueMarker : IValueRequest<int>
    { }

    private interface ISecondValueMarker : IValueRequest<int>
    { }

    private abstract record SharedValueContract : IFirstValueMarker, ISecondValueMarker;

    private abstract record PlainAndTypedValue : IValueRequest, IValueRequest<int>;

    private abstract record StringValue : IValueRequest<string>;

    private abstract record VoidValue : IValueRequest;

    private interface IAuditedValueHandler<in TRequest, TResponse>
        : IValueRequestHandler<TRequest, TResponse>
        where TRequest : IValueRequest<TResponse>
    { }

    private abstract class MatchingValueHandler : IAuditedValueHandler<StringValue, string>
    {
        public abstract ValueTask<string> HandleAsync(
            StringValue request,
            CancellationToken cancellationToken);
    }

    private abstract class VoidValueHandler : IValueRequestHandler<VoidValue>
    {
        public abstract ValueTask HandleAsync(
            VoidValue request,
            CancellationToken cancellationToken);
    }

    private abstract record WideValueRequest : IValueRequest<string>;

    private abstract class WideValueRequestHandler : IValueRequestHandler<WideValueRequest, object>
    {
        public abstract ValueTask<object> HandleAsync(
            WideValueRequest request,
            CancellationToken cancellationToken);
    }

    private abstract class ExactValueContractHandler
        : IValueRequestHandler<IValueRequest<string>, object>
    {
        public abstract ValueTask<object> HandleAsync(
            IValueRequest<string> request,
            CancellationToken cancellationToken);
    }

    private class Animal
    { }

    private sealed class Dog : Animal
    { }

    private abstract record FetchDogValue : IValueRequest<Dog>;

    private abstract class BaseValueHandler : IValueRequestHandler<FetchDogValue, Animal>
    {
        public abstract ValueTask<Animal> HandleAsync(
            FetchDogValue request,
            CancellationToken cancellationToken);
    }

    private abstract class TwoValueContractsHandler
        : IValueRequestHandler<TwoValueContracts, object>
    {
        public abstract ValueTask<object> HandleAsync(
            TwoValueContracts request,
            CancellationToken cancellationToken);
    }

    private abstract record TaskAndValue : IRequest<int>, IValueRequest<string>;

    private abstract class TaskAndValueHandler : IValueRequestHandler<TaskAndValue, object>
    {
        public abstract ValueTask<object> HandleAsync(
            TaskAndValue request,
            CancellationToken cancellationToken);
    }

    private abstract record StreamAndValue : IStreamRequest<int>, IValueRequest<string>;

    private abstract class StreamAndValueHandler : IValueRequestHandler<StreamAndValue, object>
    {
        public abstract ValueTask<object> HandleAsync(
            StreamAndValue request,
            CancellationToken cancellationToken);
    }

    #endregion
}
