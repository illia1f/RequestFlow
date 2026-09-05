using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class ValueRequestDispatcherTests
{
    [Fact]
    public async Task Given_Registered_Value_Handler_When_Sending_Request_Then_Returns_Handler_Response()
    {
        _pingHandler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string>("pong bob"));

        string result = await _sut.SendAsync(new Ping("bob"));

        result.ShouldBe("pong bob");
    }

    [Fact]
    public async Task Given_Unknown_Value_Request_When_Sending_Request_Then_Throws_Handler_Not_Found_Exception()
    {
        var exception = await Should.ThrowAsync<HandlerNotFoundException>(
            async () => await _sut.SendAsync(new Unknown()));

        exception.RequestType.ShouldBe(typeof(Unknown));
    }

    [Fact]
    public async Task Given_Covariant_Value_Response_When_Sending_Request_Then_Throws_Response_Type_Mismatch_Exception()
    {
        var exception = await Should.ThrowAsync<ResponseTypeMismatchException>(
            async () => await _sut.SendAsync<BaseResult>(new Fetch()));

        exception.RequestType.ShouldBe(typeof(Fetch));
        exception.ExpectedResponseType.ShouldBe(typeof(DerivedResult));
        exception.ActualResponseType.ShouldBe(typeof(BaseResult));
    }

    [Fact]
    public async Task Given_Null_Value_Request_When_Sending_Request_Then_Throws_Argument_Null_Exception()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await _sut.SendAsync<string>(null!));
    }

    [Fact]
    public async Task Given_Null_Void_Value_Request_When_Sending_Request_Then_Throws_Argument_Null_Exception()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await _sut.SendAsync((IValueRequest)null!));
    }

    [Fact]
    public async Task Given_Default_Value_Task_Response_When_Sending_Request_Then_Returns_Default_Response()
    {
        _pingHandler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>())
            .Returns(default(ValueTask<string>));

        string? result = await _sut.SendAsync(new Ping("bob"));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Given_A_Suspended_Typed_Value_Task_When_Sending_Request_Then_Completes_After_The_Handler()
    {
        var completion = new TaskCompletionSource<string>();
        _pingHandler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string>(completion.Task));

        ValueTask<string> result = _sut.SendAsync(new Ping("bob"));

        result.IsCompleted.ShouldBeFalse();
        completion.SetResult("pong");
        (await result).ShouldBe("pong");
    }

    [Fact]
    public async Task Given_A_Handler_That_Throws_Synchronously_When_Sending_Request_Then_The_Exception_Propagates()
    {
        var failure = new InvalidOperationException("boom");
        _pingHandler.When(handler => handler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw failure);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await _sut.SendAsync(new Ping("bob")));

        exception.ShouldBeSameAs(failure);
    }

    [Fact]
    public async Task Given_A_Frozen_Plan_Without_A_Handler_Service_When_Sending_Request_Then_DI_Failure_Propagates()
    {
        var services = Substitute.For<IServiceProvider>();
        var plans = new Dictionary<Type, RequestPlanBase>
        {
            [typeof(Ping)] = new ValueRequestPlan<Ping, string>(),
        };
        var sut = new ValueRequestDispatcher(new DispatchMap(plans), services);

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sut.SendAsync(new Ping("bob")));
    }

    [Fact]
    public async Task Given_Faulted_Value_Task_When_Sending_Request_Then_Handler_Exception_Propagates()
    {
        var failure = new InvalidOperationException("boom");
        _pingHandler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string>(Task.FromException<string>(failure)));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await _sut.SendAsync(new Ping("bob")));

        exception.ShouldBeSameAs(failure);
    }

    [Fact]
    public async Task Given_Canceled_Value_Task_When_Sending_Request_Then_Cancellation_Propagates()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        _pingHandler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string>(Task.FromCanceled<string>(source.Token)));

        var exception = await Should.ThrowAsync<OperationCanceledException>(
            async () => await _sut.SendAsync(new Ping("bob")));

        exception.CancellationToken.ShouldBe(source.Token);
    }

    [Fact]
    public async Task Given_Cancellation_Token_When_Sending_Value_Request_Then_Handler_Receives_It()
    {
        using var source = new CancellationTokenSource();

        await _sut.SendAsync(new Ping("bob"), source.Token);

        await _pingHandler.Received(1).HandleAsync(Arg.Any<Ping>(), source.Token);
    }

    [Fact]
    public async Task Given_Void_Value_Request_When_Sending_Request_Then_Standalone_Handler_Receives_It()
    {
        await _sut.SendAsync(new Log("hi"));

        await _logHandler.Received(1).HandleAsync(
            Arg.Is<Log>(request => request.Message == "hi"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Given_Synchronously_Completed_Void_Value_Task_When_Sending_Generically_Then_Returns_No_Result()
    {
        _logHandler.HandleAsync(Arg.Any<Log>(), Arg.Any<CancellationToken>())
            .Returns(default(ValueTask));

        NoResult result = await _sut.SendAsync<NoResult>(new Log("hi"));

        result.ShouldBe(NoResult.Value);
    }

    [Fact]
    public async Task Given_Incomplete_Void_Value_Task_When_Sending_Generically_Then_Completes_After_The_Handler()
    {
        var completion = new TaskCompletionSource<object?>();
        _logHandler.HandleAsync(Arg.Any<Log>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask(completion.Task));

        ValueTask<NoResult> result = _sut.SendAsync<NoResult>(new Log("hi"));

        result.IsCompleted.ShouldBeFalse();
        completion.SetResult(null);
        (await result).ShouldBe(NoResult.Value);
    }

    [Fact]
    public async Task Given_A_Completed_Void_Source_When_Completing_As_No_Result_Then_Consumes_It_Once()
    {
        var source = new SingleConsumptionValueTaskSource();
        source.SetResult();

        ValueTask<NoResult> result = ValueNoResultBridge.Complete(source.CreateValueTask());

        (await result).ShouldBe(NoResult.Value);
        source.GetResultCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Given_An_Incomplete_Void_Source_When_Completing_As_No_Result_Then_Consumes_It_Once()
    {
        var source = new SingleConsumptionValueTaskSource();

        ValueTask<NoResult> result = ValueNoResultBridge.Complete(source.CreateValueTask());

        source.GetResultCalls.ShouldBe(0);
        source.SetResult();
        (await result).ShouldBe(NoResult.Value);
        source.GetResultCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Given_A_Completed_No_Result_Source_When_Discarding_Its_Result_Then_Consumes_It_Once()
    {
        var source = new SingleConsumptionValueTaskSource<NoResult>();
        source.SetResult(NoResult.Value);

        ValueTask result = ValueNoResultBridge.Discard(source.CreateValueTask());

        await result;
        source.GetResultCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Given_An_Incomplete_No_Result_Source_When_Discarding_Its_Result_Then_Consumes_It_Once()
    {
        var source = new SingleConsumptionValueTaskSource<NoResult>();

        ValueTask result = ValueNoResultBridge.Discard(source.CreateValueTask());

        source.GetResultCalls.ShouldBe(0);
        source.SetResult(NoResult.Value);
        await result;
        source.GetResultCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Given_A_Completed_Typed_Source_When_Dispatching_Then_The_Handler_Level_Consumes_It_Once()
    {
        var source = new SingleConsumptionValueTaskSource<string>();
        source.SetResult("pong");
        _pingHandler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>())
            .Returns(source.CreateValueTask());

        string result = await _sut.SendAsync(new Ping("bob"));

        result.ShouldBe("pong");
        source.GetResultCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Given_An_Incomplete_Typed_Source_When_Dispatching_Then_The_Handler_Level_Consumes_It_Once()
    {
        var source = new SingleConsumptionValueTaskSource<string>();
        _pingHandler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>())
            .Returns(source.CreateValueTask());

        ValueTask<string> result = _sut.SendAsync(new Ping("bob"));

        source.GetResultCalls.ShouldBe(0);
        source.SetResult("pong");
        (await result).ShouldBe("pong");
        source.GetResultCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Given_A_Completed_Void_Source_When_Dispatching_Then_The_Handler_Level_Consumes_It_Once()
    {
        var source = new SingleConsumptionValueTaskSource();
        source.SetResult();
        _logHandler.HandleAsync(Arg.Any<Log>(), Arg.Any<CancellationToken>())
            .Returns(source.CreateValueTask());

        await _sut.SendAsync(new Log("hi"));

        source.GetResultCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Given_An_Incomplete_Void_Source_When_Dispatching_Then_The_Handler_Level_Consumes_It_Once()
    {
        var source = new SingleConsumptionValueTaskSource();
        _logHandler.HandleAsync(Arg.Any<Log>(), Arg.Any<CancellationToken>())
            .Returns(source.CreateValueTask());

        ValueTask result = _sut.SendAsync(new Log("hi"));

        source.GetResultCalls.ShouldBe(0);
        source.SetResult();
        await result;
        source.GetResultCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Given_A_Singleton_Handler_When_Dispatching_Twice_Then_The_Plan_Resolves_It_On_Each_Entry()
    {
        var handler = new PingHandler();
        using ServiceProvider inner = new ServiceCollection()
            .AddSingleton<IValueRequestHandler<Ping, string>>(handler)
            .BuildServiceProvider();
        var services = new CountingProvider(inner);
        var plans = new Dictionary<Type, RequestPlanBase>
        {
            [typeof(Ping)] = new ValueRequestPlan<Ping, string>(),
        };
        var sut = new ValueRequestDispatcher(new DispatchMap(plans), services);

        await sut.SendAsync(new Ping("one"));
        await sut.SendAsync(new Ping("two"));

        handler.Calls.ShouldBe(2);
        services.Requested.Count(type => type == typeof(IValueRequestHandler<Ping, string>)).ShouldBe(2);
    }

    [Fact]
    public async Task Given_A_Staged_Typed_Value_Request_When_Sending_Request_Then_The_Stage_Wraps_The_Handler()
    {
        _stagedPingHandler.HandleAsync(Arg.Any<StagedPing>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string>("pong"));

        string result = await _sut.SendAsync(new StagedPing());

        result.ShouldBe("pong:stage");
    }

    [Fact]
    public async Task Given_A_Staged_Void_Value_Request_When_Sending_Request_Then_The_Stage_And_Handler_Run()
    {
        await _sut.SendAsync(new StagedLog());

        _stagedLog.ShouldBe(["stage"]);
        await _stagedLogHandler.Received(1).HandleAsync(
            Arg.Any<StagedLog>(), Arg.Any<CancellationToken>());
    }

    #region Initialization

    private readonly IValueRequestHandler<Ping, string> _pingHandler;
    private readonly IValueRequestHandler<Log> _logHandler;
    private readonly IValueRequestHandler<StagedPing, string> _stagedPingHandler;
    private readonly IValueRequestHandler<StagedLog> _stagedLogHandler;
    private readonly List<string> _stagedLog = [];
    private readonly IValueRequestDispatcher _sut;

    public ValueRequestDispatcherTests()
    {
        _pingHandler = Substitute.For<IValueRequestHandler<Ping, string>>();
        _logHandler = Substitute.For<IValueRequestHandler<Log>>();
        _stagedPingHandler = Substitute.For<IValueRequestHandler<StagedPing, string>>();
        _stagedLogHandler = Substitute.For<IValueRequestHandler<StagedLog>>();

        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IValueRequestHandler<Ping, string>)).Returns(_pingHandler);
        services.GetService(typeof(IValueRequestHandler<Log>)).Returns(_logHandler);
        services.GetService(typeof(IValueRequestHandler<StagedPing, string>)).Returns(_stagedPingHandler);
        services.GetService(typeof(IValueRequestHandler<StagedLog>)).Returns(_stagedLogHandler);
        services.GetService(typeof(AppendingStage)).Returns(new AppendingStage());
        services.GetService(typeof(RecordingVoidStage)).Returns(new RecordingVoidStage(_stagedLog));

        var plans = new Dictionary<Type, RequestPlanBase>
        {
            [typeof(Ping)] = new ValueRequestPlan<Ping, string>(),
            [typeof(Log)] = new ValueVoidRequestPlan<Log>(),
            [typeof(Fetch)] = new ValueRequestPlan<Fetch, DerivedResult>(),
            [typeof(StagedPing)] = new StagedValueRequestPlan<StagedPing, string>(
                new StageChain([typeof(AppendingStage)], [])),
            [typeof(StagedLog)] = new StagedValueVoidRequestPlan<StagedLog>(
                new StageChain([typeof(RecordingVoidStage)], [false])),
        };
        _sut = new ValueRequestDispatcher(new DispatchMap(plans), services);
    }

    #endregion

    #region Helpers

    public sealed record Ping(string Name) : IValueRequest<string>;

    public sealed record Unknown : IValueRequest<int>;

    public sealed record Log(string Message) : IValueRequest;

    public sealed record StagedPing : IValueRequest<string>;

    public sealed record StagedLog : IValueRequest;

    public class BaseResult
    { }

    public sealed class DerivedResult : BaseResult
    { }

    public sealed record Fetch : IValueRequest<DerivedResult>;

    public sealed class PingHandler : IValueRequestHandler<Ping, string>
    {
        public int Calls { get; private set; }

        public ValueTask<string> HandleAsync(Ping request, CancellationToken cancellationToken)
        {
            Calls++;
            return new ValueTask<string>(request.Name);
        }
    }

    public sealed class UnknownHandler : IValueRequestHandler<Unknown, int>
    {
        public ValueTask<int> HandleAsync(Unknown request, CancellationToken cancellationToken)
            => new(0);
    }

    public sealed class LogHandler : IValueRequestHandler<Log>
    {
        public ValueTask HandleAsync(Log request, CancellationToken cancellationToken)
            => default;
    }

    public sealed class FetchHandler : IValueRequestHandler<Fetch, DerivedResult>
    {
        public ValueTask<DerivedResult> HandleAsync(Fetch request, CancellationToken cancellationToken)
            => new(new DerivedResult());
    }

    public sealed class StagedPingHandler : IValueRequestHandler<StagedPing, string>
    {
        public ValueTask<string> HandleAsync(StagedPing request, CancellationToken cancellationToken)
            => new("pong");
    }

    public sealed class StagedLogHandler : IValueRequestHandler<StagedLog>
    {
        public ValueTask HandleAsync(StagedLog request, CancellationToken cancellationToken)
            => default;
    }

    private sealed class AppendingStage : IValueRequestStage<StagedPing, string>
    {
        public async ValueTask<string> HandleAsync(
            StagedPing request, ValueContinuation<string> next, CancellationToken cancellationToken)
            => await next.InvokeAsync() + ":stage";
    }

    private sealed class RecordingVoidStage(List<string> log) : IValueRequestStage<StagedLog>
    {
        public ValueTask HandleAsync(
            StagedLog request, ValueContinuation next, CancellationToken cancellationToken)
        {
            log.Add("stage");
            return next.InvokeAsync();
        }
    }

    #endregion
}
