using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class AddStageTests
{
    [Fact]
    public void Given_Open_Generic_Stage_When_Adding_Stage_Then_Declaration_Is_Recorded()
    {
        _sut.AddStage(typeof(LoggingStage<,>));

        _sut.StageDeclarations.Count.ShouldBe(1);
        _sut.StageDeclarations[0].StageType.ShouldBe(typeof(LoggingStage<,>));
        _sut.StageDeclarations[0].HandlerFilter.ShouldBeNull();
    }

    [Fact]
    public void Given_Closed_Stage_When_Adding_Stage_By_Type_Argument_Then_Declaration_Is_Recorded()
    {
        _sut.AddStage<PingAuditStage>();

        _sut.StageDeclarations.Count.ShouldBe(1);
        _sut.StageDeclarations[0].StageType.ShouldBe(typeof(PingAuditStage));
    }

    [Fact]
    public void Given_Handler_Filter_When_Adding_Stage_Then_Filter_Is_Recorded()
    {
        _sut.AddStage(typeof(LoggingStage<,>), s => s.WhereHandlerImplements<IAuditable>());

        _sut.StageDeclarations[0].HandlerFilter.ShouldBe(typeof(IAuditable));
    }

    [Fact]
    public void Given_Handler_Filter_When_Adding_Stage_By_Type_Argument_Then_Filter_Is_Recorded()
    {
        _sut.AddStage<PingAuditStage>(s => s.WhereHandlerImplements<IAuditable>());

        _sut.StageDeclarations[0].StageType.ShouldBe(typeof(PingAuditStage));
        _sut.StageDeclarations[0].HandlerFilter.ShouldBe(typeof(IAuditable));
    }

    [Fact]
    public void Given_No_Declared_Lifetime_When_Adding_Stage_Then_Declaration_Is_Transient()
    {
        _sut.AddStage(typeof(LoggingStage<,>));

        _sut.StageDeclarations[0].Lifetime.ShouldBe(ServiceLifetime.Transient);
    }

    [Fact]
    public void Given_Singleton_Declared_When_Adding_Stage_Then_Lifetime_Is_Recorded()
    {
        _sut.AddStage(typeof(LoggingStage<,>), s => s.AsSingleton());

        _sut.StageDeclarations[0].Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void Given_Scoped_Declared_When_Adding_Stage_By_Type_Argument_Then_Lifetime_Is_Recorded()
    {
        _sut.AddStage<PingAuditStage>(s => s.AsScoped());

        _sut.StageDeclarations[0].Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Given_Conflicting_Lifetimes_When_Adding_Stage_Then_Throws_Invalid_Operation_Exception()
    {
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            _sut.AddStage(typeof(LoggingStage<,>), s => s.AsSingleton().AsScoped()));

        exception.Message.ShouldContain("Singleton");
        exception.Message.ShouldContain("one lifetime");
    }

    [Fact]
    public void Given_The_Same_Lifetime_Twice_When_Adding_Stage_Then_The_Declaration_Keeps_It()
    {
        _sut.AddStage(typeof(LoggingStage<,>), s => s.AsScoped().AsScoped());

        _sut.StageDeclarations[0].Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Given_Filter_And_Lifetime_When_Adding_Stage_Then_Both_Are_Recorded()
    {
        _sut.AddStage(typeof(LoggingStage<,>), s => s.WhereHandlerImplements<IAuditable>().AsScoped());

        _sut.StageDeclarations[0].HandlerFilter.ShouldBe(typeof(IAuditable));
        _sut.StageDeclarations[0].Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Given_Registration_Order_When_Adding_Stages_Then_Declarations_Keep_That_Order()
    {
        _sut.AddStage(typeof(LoggingStage<,>)).AddStage<PingAuditStage>();

        _sut.StageDeclarations[0].StageType.ShouldBe(typeof(LoggingStage<,>));
        _sut.StageDeclarations[1].StageType.ShouldBe(typeof(PingAuditStage));
    }

    [Fact]
    public void Given_Null_Stage_Type_When_Adding_Stage_Then_Throws_Argument_Null_Exception()
    {
        Should.Throw<ArgumentNullException>(() => _sut.AddStage(null!));
    }

    [Fact]
    public void Given_Valid_Declarations_When_Validating_Then_All_Are_Valid_And_No_Problems()
    {
        StageDeclaration[] declarations =
        [
            new StageDeclaration(typeof(LoggingStage<,>), null, StageFamily.Request),
            new StageDeclaration(typeof(PingAuditStage), null, StageFamily.Request),
        ];

        Validated<StageDeclaration> result = RegistrationValidator.ValidateStageDeclarations(declarations);

        result.Valid.Count.ShouldBe(2);
        result.Problems.ShouldBeEmpty();
    }

    [Fact]
    public void Given_Stage_Whose_Parameter_Is_Not_The_Request_When_Validating_Then_Reports_The_Parameter()
    {
        StageDeclaration[] declarations = [new StageDeclaration(typeof(OneParameterStage<>), null, StageFamily.Request)];

        Validated<StageDeclaration> result = RegistrationValidator.ValidateStageDeclarations(declarations);

        result.Valid.ShouldBeEmpty();
        result.Problems.Count.ShouldBe(1);
        result.Problems[0].Code.ShouldBe("RF0012");
        result.Problems[0].Subject.ShouldBe(typeof(OneParameterStage<>));
        result.Problems[0].Message.ShouldContain("does not use as its request");
    }

    [Fact]
    public void Given_Type_That_Is_Not_A_Stage_When_Validating_Then_Reports_Missing_Contract()
    {
        StageDeclaration[] declarations = [new StageDeclaration(typeof(NotAStage), null, StageFamily.Request)];

        Validated<StageDeclaration> result = RegistrationValidator.ValidateStageDeclarations(declarations);

        result.Valid.ShouldBeEmpty();
        result.Problems[0].Code.ShouldBe("RF0011");
        result.Problems[0].Subject.ShouldBe(typeof(NotAStage));
        result.Problems[0].Message.ShouldContain("does not implement IRequestStage");
    }

    [Fact]
    public void Given_Abstract_Stage_When_Validating_Then_Reports_Abstract()
    {
        StageDeclaration[] declarations = [new StageDeclaration(typeof(AbstractStage), null, StageFamily.Request)];

        Validated<StageDeclaration> result = RegistrationValidator.ValidateStageDeclarations(declarations);

        result.Valid.ShouldBeEmpty();
        result.Problems[0].Code.ShouldBe("RF0009");
        result.Problems[0].Subject.ShouldBe(typeof(AbstractStage));
        result.Problems[0].Message.ShouldContain("is abstract");
    }

    [Fact]
    public void Given_Handler_Filter_Already_Set_When_Adding_A_Second_One_Then_Throws_Invalid_Operation_Exception()
    {
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            _sut.AddStage(typeof(LoggingStage<,>), s => s
                .WhereHandlerImplements<IAuditable>()
                .WhereHandlerImplements<ITag>()));

        exception.Message.ShouldContain(nameof(IAuditable));
        exception.Message.ShouldContain("one handler filter");
    }

    [Fact]
    public void Given_Stage_With_Swapped_Generic_Parameters_When_Validating_Then_Reports_The_Parameter_Order()
    {
        StageDeclaration[] declarations = [new StageDeclaration(typeof(SwappedStage<,>), null, StageFamily.Request)];

        Validated<StageDeclaration> result = RegistrationValidator.ValidateStageDeclarations(declarations);

        result.Valid.ShouldBeEmpty();
        result.Problems[0].Code.ShouldBe("RF0012");
        result.Problems[0].Subject.ShouldBe(typeof(SwappedStage<,>));
        result.Problems[0].Message.ShouldContain("in that order");
    }

    [Fact]
    public void Given_Open_Void_Form_Stage_When_Validating_Then_Declaration_Is_Valid()
    {
        StageDeclaration[] declarations = [new StageDeclaration(typeof(VoidOnlyStage<>), null, StageFamily.Request)];

        Validated<StageDeclaration> result = RegistrationValidator.ValidateStageDeclarations(declarations);

        result.Valid.Count.ShouldBe(1);
        result.Problems.ShouldBeEmpty();
    }

    [Fact]
    public void Given_Closed_Void_Form_Stage_When_Validating_Then_Declaration_Is_Valid()
    {
        StageDeclaration[] declarations = [new StageDeclaration(typeof(WipeAuditStage), null, StageFamily.Request)];

        Validated<StageDeclaration> result = RegistrationValidator.ValidateStageDeclarations(declarations);

        result.Valid.Count.ShouldBe(1);
        result.Problems.ShouldBeEmpty();
    }

    [Fact]
    public void Given_Open_Response_Bound_Stage_When_Validating_Then_Declaration_Is_Valid()
    {
        StageDeclaration[] declarations = [new StageDeclaration(typeof(ResponseBoundStage<>), null, StageFamily.Request)];

        Validated<StageDeclaration> result = RegistrationValidator.ValidateStageDeclarations(declarations);

        result.Valid.Count.ShouldBe(1);
        result.Problems.ShouldBeEmpty();
    }

    [Fact]
    public void Given_Single_Parameter_Stage_Closed_Over_Its_Request_When_Validating_Then_Declaration_Is_Valid()
    {
        Type closed = typeof(ResponseBoundStage<>).MakeGenericType(typeof(Ping));
        StageDeclaration[] declarations = [new StageDeclaration(closed, null, StageFamily.Request)];

        Validated<StageDeclaration> result = RegistrationValidator.ValidateStageDeclarations(declarations);

        result.Valid.Count.ShouldBe(1);
        result.Problems.ShouldBeEmpty();
    }

    [Fact]
    public void Given_Interface_Stage_When_Validating_Then_Reports_Interface()
    {
        StageDeclaration[] declarations = [new StageDeclaration(typeof(IRequestStage<Ping, string>), null, StageFamily.Request)];

        Validated<StageDeclaration> result = RegistrationValidator.ValidateStageDeclarations(declarations);

        result.Valid.ShouldBeEmpty();
        result.Problems[0].Code.ShouldBe("RF0008");
        result.Problems[0].Subject.ShouldBe(typeof(IRequestStage<Ping, string>));
        result.Problems[0].Message.ShouldContain("is an interface");
    }

    [Fact]
    public void Given_Partially_Closed_Stage_When_Validating_Then_Reports_Partially_Closed()
    {
        Type openParameter = typeof(List<>).GetGenericArguments()[0];
        Type partiallyClosed = typeof(OneParameterStage<>).MakeGenericType(openParameter);
        StageDeclaration[] declarations = [new StageDeclaration(partiallyClosed, null, StageFamily.Request)];

        Validated<StageDeclaration> result = RegistrationValidator.ValidateStageDeclarations(declarations);

        result.Valid.ShouldBeEmpty();
        result.Problems[0].Code.ShouldBe("RF0010");
        result.Problems[0].Subject.ShouldBe(partiallyClosed);
        result.Problems[0].Message.ShouldContain("is partially closed");
    }

    #region Initialization

    private readonly RequestFlowOptions _sut = new();

    #endregion

    #region Helpers

    public sealed record Ping(string Text) : IRequest<string>;

    public sealed record Wipe : IRequest;

    // The assembly scan demands one handler per request type, even for Ping, never dispatched here.
    public sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, CancellationToken cancellationToken)
            => Task.FromResult(request.Text);
    }

    public sealed class WipeHandler : IRequestHandler<Wipe>
    {
        public Task HandleAsync(Wipe request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private interface IAuditable
    { }

    private interface ITag
    { }

    private sealed class LoggingStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    private sealed class PingAuditStage : IRequestStage<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, Continuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    // One type parameter that the contract never uses as its request, so validation rejects
    // it even though it implements IRequestStage.
    private sealed class OneParameterStage<TRequest> : IRequestStage<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, Continuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    private sealed class ResponseBoundStage<TRequest> : IRequestStage<TRequest, string>
        where TRequest : IRequest<string>
    {
        public Task<string> HandleAsync(TRequest request, Continuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    // Implements the contract, but with the parameters transposed, so closing it over a
    // request produces a stage no request can match.
    private sealed class SwappedStage<TResponse, TRequest> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    private sealed class VoidOnlyStage<TRequest> : IRequestStage<TRequest>
        where TRequest : IRequest
    {
        public Task HandleAsync(TRequest request, Continuation next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    private sealed class WipeAuditStage : IRequestStage<Wipe>
    {
        public Task HandleAsync(Wipe request, Continuation next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    private sealed class NotAStage
    { }

    private abstract class AbstractStage : IRequestStage<Ping, string>
    {
        public abstract Task<string> HandleAsync(Ping request, Continuation<string> next, CancellationToken cancellationToken);
    }

    #endregion
}
