using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class RequestDispatcherTests
{
    [Fact]
    public async Task Given_Registered_Handler_When_Sending_Request_Then_Handler_Receives_Request()
    {
        await _sut.SendAsync(new Ping("bob"));

        await _pingHandler.Received(1).HandleAsync(
            Arg.Is<Ping>(p => p.Name == "bob"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Given_Registered_Handler_When_Sending_Request_Then_Returns_Handler_Response()
    {
        _pingHandler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>())
            .Returns("pong bob");

        string result = await _sut.SendAsync(new Ping("bob"));

        result.ShouldBe("pong bob");
    }

    [Fact]
    public async Task Given_Unknown_Request_Type_When_Sending_Request_Then_Throws_Handler_Not_Found_Exception()
    {
        var exception = await Should.ThrowAsync<HandlerNotFoundException>(
            () => _sut.SendAsync(new Unknown()));

        exception.RequestType.ShouldBe(typeof(Unknown));
    }

    [Fact]
    public async Task Given_Void_Request_When_Sending_Request_Then_Standalone_Handler_Receives_Request()
    {
        await _sut.SendAsync(new Log("hi"));

        await _logHandler.Received(1).HandleAsync(
            Arg.Is<Log>(l => l.Message == "hi"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Given_Synchronously_Completing_Void_Handler_When_Sending_Request_Then_Returns_Cached_No_Result_Task()
    {
        _logHandler.HandleAsync(Arg.Any<Log>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        Task<NoResult> result = _sut.SendAsync<NoResult>(new Log("hi"));

        result.ShouldBeSameAs(NoResult.Task);
        await result;
    }

    [Fact]
    public async Task Given_Covariant_Response_Type_When_Sending_Request_Then_Throws_Response_Type_Mismatch_Exception()
    {
        var exception = await Should.ThrowAsync<ResponseTypeMismatchException>(
            () => _sut.SendAsync<object>(new Ping("bob")));

        exception.RequestType.ShouldBe(typeof(Ping));
        exception.ExpectedResponseType.ShouldBe(typeof(string));
        exception.ActualResponseType.ShouldBe(typeof(object));
    }

    [Fact]
    public async Task Given_Base_Class_Response_Type_When_Sending_Request_Then_Throws_Response_Type_Mismatch_Exception()
    {
        var exception = await Should.ThrowAsync<ResponseTypeMismatchException>(
            () => _sut.SendAsync<BaseResult>(new Fetch()));

        exception.RequestType.ShouldBe(typeof(Fetch));
        exception.ExpectedResponseType.ShouldBe(typeof(DerivedResult));
        exception.ActualResponseType.ShouldBe(typeof(BaseResult));
    }

    [Fact]
    public async Task Given_Failed_Mismatched_Send_When_Sending_Correctly_Typed_Request_Then_Returns_Handler_Response()
    {
        _pingHandler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>())
            .Returns("pong");
        await Should.ThrowAsync<ResponseTypeMismatchException>(
            () => _sut.SendAsync<object>(new Ping("bob")));

        string result = await _sut.SendAsync(new Ping("bob"));

        result.ShouldBe("pong");
    }

    [Fact]
    public async Task Given_Null_Request_When_Sending_Request_Then_Throws_Argument_Null_Exception()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.SendAsync<string>(null!));
    }

    #region Initialization

    private readonly IRequestHandler<Ping, string> _pingHandler;
    private readonly IRequestHandler<Log> _logHandler;
    private readonly RequestDispatcher _sut;

    public RequestDispatcherTests()
    {
        _pingHandler = Substitute.For<IRequestHandler<Ping, string>>();
        _logHandler = Substitute.For<IRequestHandler<Log>>();

        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IRequestHandler<Ping, string>)).Returns(_pingHandler);
        services.GetService(typeof(IRequestHandler<Log>)).Returns(_logHandler);

        // The map omits Unknown on purpose so dispatch can miss.
        var plans = new Dictionary<Type, RequestPlanBase>
        {
            [typeof(Ping)] = new RequestPlan<Ping, string>(),
            [typeof(Log)] = new VoidRequestPlan<Log>(),
            [typeof(Fetch)] = new RequestPlan<Fetch, DerivedResult>(),
        };
        _sut = new RequestDispatcher(new DispatchMap(plans), services);
    }

    #endregion

    #region Helpers

    // Public so NSubstitute can proxy handler interfaces closed over these types.
    public sealed record Ping(string Name) : IRequest<string>;

    public sealed record Unknown : IRequest<int>;

    public sealed record Log(string Message) : IRequest;

    public class BaseResult
    { }

    public sealed class DerivedResult : BaseResult
    { }

    // The registered response type is DerivedResult; sending as SendAsync<BaseResult>
    // compiles through covariance and must fail with the dedicated mismatch exception.
    public sealed record Fetch : IRequest<DerivedResult>;

    // AddRequestFlow scans this assembly and demands a handler per request type, so each
    // fixture record needs a concrete handler even though the tests only use the mocks.
    public sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, CancellationToken cancellationToken)
            => Task.FromResult(request.Name);
    }

    public sealed class UnknownHandler : IRequestHandler<Unknown, int>
    {
        public Task<int> HandleAsync(Unknown request, CancellationToken cancellationToken)
            => Task.FromResult(0);
    }

    public sealed class LogHandler : IRequestHandler<Log>
    {
        public Task HandleAsync(Log request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class FetchHandler : IRequestHandler<Fetch, DerivedResult>
    {
        public Task<DerivedResult> HandleAsync(Fetch request, CancellationToken cancellationToken)
            => Task.FromResult(new DerivedResult());
    }

    #endregion
}
