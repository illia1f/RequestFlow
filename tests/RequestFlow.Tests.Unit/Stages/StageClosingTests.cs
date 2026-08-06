using Microsoft.Extensions.DependencyInjection;
using RequestFlow;
using RequestFlow.Tests.ValidationFixtures;

namespace RequestFlow.Tests.Unit;

public sealed class StageClosingTests
{
    [Fact]
    public void Given_Unconstrained_Open_Stage_When_Closing_Over_A_Request_Then_Applies_With_Closed_Type()
    {
        var declaration = new StageDeclaration(typeof(LoggingStage<,>), null);

        bool applies = StageClosing.TryClose(declaration, _pingHandler, out Type closedStageType, out string reason);

        applies.ShouldBeTrue();
        closedStageType.ShouldBe(typeof(LoggingStage<Ping, string>));
        reason.ShouldContain("generic constraints");
    }

    [Fact]
    public void Given_Constrained_Open_Stage_When_Request_Violates_The_Constraint_Then_Does_Not_Apply()
    {
        var declaration = new StageDeclaration(typeof(TaggedOnlyStage<,>), null);

        bool applies = StageClosing.TryClose(declaration, _pingHandler, out _, out _);

        applies.ShouldBeFalse();
    }

    [Fact]
    public void Given_Constrained_Open_Stage_When_Request_Satisfies_The_Constraint_Then_Applies()
    {
        var declaration = new StageDeclaration(typeof(TaggedOnlyStage<,>), null);

        bool applies = StageClosing.TryClose(declaration, _taggedHandler, out Type closedStageType, out _);

        applies.ShouldBeTrue();
        closedStageType.ShouldBe(typeof(TaggedOnlyStage<Tagged, string>));
    }

    [Fact]
    public void Given_Closed_Stage_When_Request_Matches_Its_Contract_Then_Applies_Without_Closing()
    {
        var declaration = new StageDeclaration(typeof(PingAuditStage), null);

        bool applies = StageClosing.TryClose(declaration, _pingHandler, out Type closedStageType, out string reason);

        applies.ShouldBeTrue();
        closedStageType.ShouldBe(typeof(PingAuditStage));
        reason.ShouldContain("closed stage");
    }

    [Fact]
    public void Given_Closed_Stage_When_Request_Does_Not_Match_Its_Contract_Then_Does_Not_Apply()
    {
        var declaration = new StageDeclaration(typeof(PingAuditStage), null);

        bool applies = StageClosing.TryClose(declaration, _taggedHandler, out _, out _);

        applies.ShouldBeFalse();
    }

    [Fact]
    public void Given_Handler_Filter_When_Handler_Implements_The_Contract_Then_Applies()
    {
        var declaration = new StageDeclaration(typeof(LoggingStage<,>), typeof(IAuditable));

        bool applies = StageClosing.TryClose(declaration, _taggedHandler, out Type closedStageType, out string reason);

        applies.ShouldBeTrue();
        closedStageType.ShouldBe(typeof(LoggingStage<Tagged, string>));
        reason.ShouldContain(nameof(IAuditable));
    }

    [Fact]
    public void Given_Handler_Filter_When_Handler_Does_Not_Implement_The_Contract_Then_Does_Not_Apply()
    {
        var declaration = new StageDeclaration(typeof(LoggingStage<,>), typeof(IAuditable));

        bool applies = StageClosing.TryClose(declaration, _pingHandler, out _, out _);

        applies.ShouldBeFalse();
    }

    [Fact]
    public void Given_Void_Request_When_Closing_An_Open_Stage_Then_Applies_Over_No_Result()
    {
        var declaration = new StageDeclaration(typeof(LoggingStage<,>), null);

        bool applies = StageClosing.TryClose(declaration, _logHandler, out Type closedStageType, out _);

        applies.ShouldBeTrue();
        closedStageType.ShouldBe(typeof(LoggingStage<Log, NoResult>));
    }

    [Fact]
    public void Given_Stage_Registered_Before_A_Later_Assembly_Scan_When_Adding_Request_Flow_Twice_Then_Closed_Stage_Is_Registered_For_Both()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<StageClosingTests>()
            .AddStage(typeof(LoggingStage<,>)));
        services.AddRequestFlow(o => o.RegisterHandlersFromAssembly(typeof(Rooted).Assembly));

        services.ShouldContain(d => d.ServiceType == typeof(LoggingStage<Ping, string>));
        services.ShouldContain(d => d.ServiceType == typeof(LoggingStage<Rooted, int>));
    }

    [Fact]
    public void Given_Same_Closed_Stage_Reached_By_Two_Calls_When_Adding_Request_Flow_Then_It_Is_Registered_Once()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<StageClosingTests>()
            .AddStage(typeof(LoggingStage<,>)));
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<StageClosingTests>()
            .AddStage(typeof(LoggingStage<,>)));

        services.Count(d => d.ServiceType == typeof(LoggingStage<Ping, string>)).ShouldBe(1);
    }

    #region Initialization

    private readonly HandlerRegistration _pingHandler =
        new(typeof(PingHandler), typeof(Ping), typeof(string), isVoid: false);

    private readonly HandlerRegistration _taggedHandler =
        new(typeof(TaggedHandler), typeof(Tagged), typeof(string), isVoid: false);

    private readonly HandlerRegistration _logHandler =
        new(typeof(LogHandler), typeof(Log), typeof(NoResult), isVoid: true);

    #endregion

    #region Helpers

    public interface ITag
    { }

    public interface IAuditable
    { }

    public sealed record Ping(string Text) : IRequest<string>;

    public sealed record Tagged(string Text) : IRequest<string>, ITag;

    public sealed record Log : IRequest;

    // The assembly scan demands one handler per request type, and other test classes run it.
    public sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, CancellationToken cancellationToken)
            => Task.FromResult(request.Text);
    }

    public sealed class TaggedHandler : IRequestHandler<Tagged, string>, IAuditable
    {
        public Task<string> HandleAsync(Tagged request, CancellationToken cancellationToken)
            => Task.FromResult(request.Text);
    }

    public sealed class LogHandler : IRequestHandler<Log>
    {
        public Task HandleAsync(Log request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class LoggingStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    private sealed class TaggedOnlyStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>, ITag
    {
        public Task<TResponse> HandleAsync(TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    private sealed class PingAuditStage : IRequestStage<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, Continuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    #endregion
}
