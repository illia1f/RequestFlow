using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class HandlerResponseMismatchRuleTests
{
    [Fact]
    public void Given_A_Handler_With_A_Wider_Response_Type_When_Validating_Then_Reports_The_Handler()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringAsk), r => r.AddHandler(typeof(WideResponseHandler), typeof(object)))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0112");
        problem.Subject.ShouldBe(typeof(WideResponseHandler));
        problem.Message.ShouldContain("System.Object");
        problem.Message.ShouldContain("System.String");
    }

    [Fact]
    public void Given_A_Handler_With_A_Base_Class_Response_Type_When_Validating_Then_Reports_The_Handler()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(FetchDog), r => r.AddHandler(typeof(BaseResponseHandler), typeof(Animal)))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0112");
        problem.Subject.ShouldBe(typeof(BaseResponseHandler));
    }

    [Fact]
    public void Given_A_Handler_With_The_Declared_Response_Type_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringAsk), r => r.AddHandler(typeof(MatchingResponseHandler), typeof(string)))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Void_Handler_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(VoidAsk), r => r.AddHandler(typeof(VoidAskHandler)))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Request_With_No_Handler_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringAsk))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    // RF0106 owns the ambiguity, and no single contract names the response a handler has to match.
    [Fact]
    public void Given_A_Request_With_Two_Response_Contracts_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(TwoContracts), r => r.AddHandler(typeof(WideResponseHandler), typeof(object)))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    // RF0109 owns a type carrying both families; the stream handler's item type is not a response.
    [Fact]
    public void Given_A_Stream_Handler_On_A_Request_Of_Both_Families_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(
                typeof(BothFamilies),
                r => r.AddHandler(typeof(BothFamiliesStreamHandler), typeof(int), typeof(IStreamRequestHandler<,>)))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    // RF0126 owns a type carrying Task and ValueTask contracts, so there is no sole Task response.
    [Fact]
    public void Given_A_Wide_Task_Handler_On_A_Task_And_Value_Request_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(
                typeof(TaskAndValue),
                request => request.AddHandler(
                    typeof(TaskAndValueHandler),
                    typeof(object),
                    typeof(IRequestHandler<,>)))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Scanned_Handler_With_A_Wider_Response_Type_When_Resolving_Dispatcher_Then_The_Problem_Is_In_The_Exception()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssembly(
            typeof(RequestFlow.Tests.ValidationFixtures.Lonely).Assembly));

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        exception.Problems.ShouldContain(p =>
            p.Code == "RF0112" && p.Subject == typeof(RequestFlow.Tests.ValidationFixtures.WideHandler));
    }

    #region Initialization

    private readonly HandlerResponseMismatchRule _sut = new();

    #endregion

    #region Helpers

    // Abstract keeps these out of the scanner when other tests scan this assembly; the rule reads
    // a type's interfaces only.
    private abstract record StringAsk : IRequest<string>;

    private abstract record VoidAsk : IRequest;

    private abstract record TwoContracts : IRequest<string>, IRequest<int>;

    private abstract record BothFamilies : IRequest<string>, IStreamRequest<int>;

    private abstract record TaskAndValue : IRequest<string>, IValueRequest<int>;

    // Compiles because IRequest<TResponse> is covariant: StringAsk satisfies IRequest<object>, so
    // the handler constraint closes over the wider response.
    private abstract class WideResponseHandler : IRequestHandler<StringAsk, object>
    {
        public abstract Task<object> HandleAsync(StringAsk request, CancellationToken cancellationToken);
    }

    private abstract class MatchingResponseHandler : IRequestHandler<StringAsk, string>
    {
        public abstract Task<string> HandleAsync(StringAsk request, CancellationToken cancellationToken);
    }

    private abstract class VoidAskHandler : IRequestHandler<VoidAsk>
    {
        public abstract Task HandleAsync(VoidAsk request, CancellationToken cancellationToken);
    }

    private class Animal
    { }

    private sealed class Dog : Animal
    { }

    private abstract record FetchDog : IRequest<Dog>;

    private abstract class BaseResponseHandler : IRequestHandler<FetchDog, Animal>
    {
        public abstract Task<Animal> HandleAsync(FetchDog request, CancellationToken cancellationToken);
    }

    private abstract class BothFamiliesStreamHandler : IStreamRequestHandler<BothFamilies, int>
    {
        public abstract IAsyncEnumerable<int> Handle(BothFamilies request, CancellationToken cancellationToken);
    }

    private abstract class TaskAndValueHandler : IRequestHandler<TaskAndValue, object>
    {
        public abstract Task<object> HandleAsync(
            TaskAndValue request,
            CancellationToken cancellationToken);
    }

    #endregion
}
