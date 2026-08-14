using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class StageResponseMismatchRuleTests
{
    [Fact]
    public void Given_A_Closed_Stage_With_A_Wider_Response_Type_When_Validating_Then_Reports_The_Stage()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringAsk))
            .AddStageDeclaration(typeof(WideResponseStage))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0113");
        problem.Subject.ShouldBe(typeof(WideResponseStage));
        problem.Message.ShouldContain("System.Object");
        problem.Message.ShouldContain("System.String");
    }

    [Fact]
    public void Given_A_Closed_Stage_With_The_Declared_Response_Type_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringAsk))
            .AddStageDeclaration(typeof(MatchingResponseStage))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Stage_Naming_A_Base_Request_When_A_Derived_Request_Is_Registered_Then_Reports_The_Stage()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(DerivedAsk))
            .AddStageDeclaration(typeof(WideResponseStage))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0113");
        problem.Subject.ShouldBe(typeof(WideResponseStage));
    }

    [Fact]
    public void Given_An_Open_Generic_Stage_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringAsk))
            .AddStageDeclaration(typeof(OpenStage<,>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_An_Open_One_Parameter_Stage_With_A_Wider_Response_Type_When_Validating_Then_Reports_The_Stage()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringAsk))
            .AddStageDeclaration(typeof(OpenWideResponseStage<>))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0113");
        problem.Subject.ShouldBe(typeof(OpenWideResponseStage<>));
        problem.Message.ShouldContain("System.Object");
        problem.Message.ShouldContain("System.String");
    }

    [Fact]
    public void Given_An_Open_One_Parameter_Stage_With_The_Declared_Response_Type_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringAsk))
            .AddStageDeclaration(typeof(OpenMatchingResponseStage<>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_An_Open_One_Parameter_Stage_Whose_Constraints_Exclude_The_Request_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringAsk))
            .AddStageDeclaration(typeof(OpenIntResponseStage<>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Void_Stage_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(VoidAsk))
            .AddStageDeclaration(typeof(VoidStage), typeof(IRequestStage<>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Stage_Naming_An_Unregistered_Request_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(OtherAsk))
            .AddStageDeclaration(typeof(WideResponseStage))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Stream_Stage_Declaration_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringStream))
            .AddStageDeclaration(typeof(StreamStage), typeof(IStreamRequestStage<,>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Request_With_Two_Response_Contracts_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(TwoContracts))
            .AddStageDeclaration(typeof(TwoContractsStage))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Wide_Response_Stage_Wrapping_Another_Request_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(ObjectAsk), r => r.AddStage(
                typeof(OpenWideResponseStage<>),
                typeof(OpenWideResponseStage<ObjectAsk>)))
            .AddRequest(typeof(StringAsk))
            .AddStageDeclaration(typeof(OpenWideResponseStage<>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Wide_Response_Stage_When_Resolving_The_Dispatcher_Then_The_Problem_Is_In_The_Exception()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StageResponseMismatchRuleTests>();
            o.AddStage<WidePingStage>();
        });

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0113");
        problem.Subject.ShouldBe(typeof(WidePingStage));
    }

    [Fact]
    public void Given_A_Filtered_Wide_Response_Stage_Whose_Filter_Excludes_Every_Handler_When_Resolving_The_Dispatcher_Then_Nothing_Throws()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StageResponseMismatchRuleTests>();
            o.AddStage(typeof(MarkedWideStage<>), s => s.WhereHandlerImplements<IUnmatched>());
        });

        Should.NotThrow(() => services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());
    }

    [Fact]
    public void Given_A_Filtered_Wide_Response_Stage_Whose_Filter_Admits_A_Narrower_Handler_When_Resolving_The_Dispatcher_Then_The_Problem_Is_In_The_Exception()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StageResponseMismatchRuleTests>();
            o.AddStage(typeof(MarkedWideStage<>), s => s.WhereHandlerImplements<INarrowMarked>());
        });

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0113");
        problem.Subject.ShouldBe(typeof(MarkedWideStage<>));
    }

    #region Initialization

    private readonly StageResponseMismatchRule _sut = new();

    #endregion

    #region Helpers

    // Abstract keeps these out of the scanner when other tests scan this assembly; the rule reads a
    // type's interfaces only.
    private abstract record StringAsk : IRequest<string>;

    private abstract record DerivedAsk : StringAsk;

    private abstract record OtherAsk : IRequest<string>;

    private abstract record ObjectAsk : IRequest<object>;

    private abstract record VoidAsk : IRequest;

    private abstract record TwoContracts : IRequest<string>, IRequest<int>;

    private abstract record StringStream : IStreamRequest<string>;

    // Compiles because IRequest<TResponse> is covariant: StringAsk satisfies IRequest<object>, so the
    // stage constraint closes over the wider response.
    private abstract class WideResponseStage : IRequestStage<StringAsk, object>
    {
        public abstract Task<object> HandleAsync(
            StringAsk request, Continuation<object> next, CancellationToken cancellationToken);
    }

    private abstract class MatchingResponseStage : IRequestStage<StringAsk, string>
    {
        public abstract Task<string> HandleAsync(
            StringAsk request, Continuation<string> next, CancellationToken cancellationToken);
    }

    private abstract class OpenStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public abstract Task<TResponse> HandleAsync(
            TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken);
    }

    // The one-parameter form of WideResponseStage: the class fixes the wider response, and covariance
    // lets the constraint admit requests that declare a narrower one.
    private abstract class OpenWideResponseStage<TRequest> : IRequestStage<TRequest, object>
        where TRequest : IRequest<object>
    {
        public abstract Task<object> HandleAsync(
            TRequest request, Continuation<object> next, CancellationToken cancellationToken);
    }

    private abstract class OpenMatchingResponseStage<TRequest> : IRequestStage<TRequest, string>
        where TRequest : IRequest<string>
    {
        public abstract Task<string> HandleAsync(
            TRequest request, Continuation<string> next, CancellationToken cancellationToken);
    }

    // Value-type responses get no covariance, so this constraint excludes StringAsk outright.
    private abstract class OpenIntResponseStage<TRequest> : IRequestStage<TRequest, int>
        where TRequest : IRequest<int>
    {
        public abstract Task<int> HandleAsync(
            TRequest request, Continuation<int> next, CancellationToken cancellationToken);
    }

    private abstract class TwoContractsStage : IRequestStage<TwoContracts, string>
    {
        public abstract Task<string> HandleAsync(
            TwoContracts request, Continuation<string> next, CancellationToken cancellationToken);
    }

    private abstract class VoidStage : IRequestStage<VoidAsk>
    {
        public abstract Task HandleAsync(VoidAsk request, Continuation next, CancellationToken cancellationToken);
    }

    private abstract class StreamStage : IStreamRequestStage<StringStream, string>
    {
        public abstract IAsyncEnumerable<string> Handle(
            StringStream request, StreamContinuation<string> next, CancellationToken cancellationToken);
    }

    public sealed record Ping : IRequest<string>;

    public sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, CancellationToken cancellationToken)
            => Task.FromResult("ping");
    }

    public sealed class WidePingStage : IRequestStage<Ping, object>
    {
        public Task<object> HandleAsync(Ping request, Continuation<object> next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    public interface IMarked
    { }

    // Deliberately implemented by no handler, so a filter on it leaves the stage wrapping nothing.
    public interface IUnmatched
    { }

    // Marks the one handler whose request declares a response narrower than the wide stage takes.
    public interface INarrowMarked
    { }

    public sealed record Tag : IRequest<string>;

    public sealed class TagHandler : IRequestHandler<Tag, string>, INarrowMarked
    {
        public Task<string> HandleAsync(Tag request, CancellationToken cancellationToken)
            => Task.FromResult("tag");
    }

    public sealed record Box : IRequest<object>;

    public sealed class BoxHandler : IRequestHandler<Box, object>, IMarked
    {
        public Task<object> HandleAsync(Box request, CancellationToken cancellationToken)
            => Task.FromResult<object>("box");
    }

    // Covariance admits Ping, whose handler the filter excludes, so only Box is wrapped.
    public sealed class MarkedWideStage<TRequest> : IRequestStage<TRequest, object>
        where TRequest : IRequest<object>
    {
        public Task<object> HandleAsync(TRequest request, Continuation<object> next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    #endregion
}
