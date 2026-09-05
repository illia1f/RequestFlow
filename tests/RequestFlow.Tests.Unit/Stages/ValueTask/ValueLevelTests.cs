using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class ValueLevelTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_A_Typed_Value_Stage_Level_When_Entered_Then_The_Stage_Receives_The_Request_And_Token(
        bool completeBeforeEntry)
    {
        var source = new SingleConsumptionValueTaskSource<string>();
        if (completeBeforeEntry)
            source.SetResult("below");

        var stage = new ForwardingTypedStage();
        ValueLevelEntry<string> root = ValueLevelFactory.Stage<Ping, string>(
            typeof(ForwardingTypedStage),
            (request, services, cancellationToken) => source.CreateValueTask());
        using var tokenSource = new CancellationTokenSource();

        ValueTask<string> pending = root(new Ping("hi"), Provider(stage), tokenSource.Token);
        if (!completeBeforeEntry)
        {
            source.GetResultCalls.ShouldBe(0);
            source.SetResult("below");
        }

        string result = await pending;

        result.ShouldBe("below");
        stage.Request.ShouldBe(new Ping("hi"));
        stage.Token.ShouldBe(tokenSource.Token);
        source.GetResultCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_A_Plain_Void_Value_Stage_Level_When_Entered_Then_It_Returns_Plain_Value_Task(
        bool completeBeforeEntry)
    {
        var source = new SingleConsumptionValueTaskSource();
        if (completeBeforeEntry)
            source.SetResult();

        ValueVoidLevelEntry root = ValueLevelFactory.VoidStage<Log>(
            typeof(ForwardingPlainVoidStage),
            (request, services, cancellationToken) => source.CreateValueTask());

        ValueTask pending = root(
            new Log(), Provider(new ForwardingPlainVoidStage()), CancellationToken.None);
        if (!completeBeforeEntry)
        {
            source.GetResultCalls.ShouldBe(0);
            source.SetResult();
        }

        await pending;

        source.GetResultCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Given_A_Typed_No_Result_Value_Stage_In_A_Void_Chain_When_Entered_Then_Both_Bridges_Consume_Once()
    {
        var belowSource = new SingleConsumptionValueTaskSource();
        belowSource.SetResult();
        var stageSource = new SingleConsumptionValueTaskSource<NoResult>();
        stageSource.SetResult(NoResult.Value);
        var stage = new BridgedTypedVoidStage(stageSource);
        ValueVoidLevelEntry root = ValueLevelFactory.TypedVoidStage<Log>(
            typeof(BridgedTypedVoidStage),
            (request, services, cancellationToken) => belowSource.CreateValueTask());

        ValueTask result = root(new Log(), Provider(stage), CancellationToken.None);

        await result;
        belowSource.GetResultCalls.ShouldBe(1);
        stageSource.GetResultCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Given_A_Two_Stage_Value_Chain_When_Entered_Then_Registration_Order_Is_Outer_To_Inner()
    {
        List<string> log = [];
        ServiceProvider provider = Provider(
            new OuterStage(log), new InnerStage(log), new PingHandler(log));
        ValueLevelEntry<string> root = ValueChainBuilder.Typed<Ping, string>(
            new StageChain([typeof(OuterStage), typeof(InnerStage)], []));

        string result = await root(new Ping("hi"), provider, CancellationToken.None);

        result.ShouldBe("hi");
        log.ShouldBe(["outer", "inner", "handler"]);
    }

    [Fact]
    public async Task Given_A_Mixed_Void_Value_Chain_When_Entered_Then_Typed_And_Plain_Stages_Run_In_Order()
    {
        List<string> log = [];
        ServiceProvider provider = Provider(
            new TypedVoidStage(log), new PlainVoidStage(log), new LogHandler(log));
        ValueVoidLevelEntry root = ValueChainBuilder.Void<Log>(
            new StageChain([typeof(TypedVoidStage), typeof(PlainVoidStage)], [true, false]));

        await root(new Log(), provider, CancellationToken.None);

        log.ShouldBe(["typed", "plain", "handler"]);
    }

    [Fact]
    public async Task Given_A_Value_Stage_That_Short_Circuits_When_Entered_Then_The_Handler_Is_Not_Resolved()
    {
        ServiceProvider provider = Provider(new ShortCircuitStage());
        ValueLevelEntry<string> root = ValueChainBuilder.Typed<Ping, string>(
            new StageChain([typeof(ShortCircuitStage)], []));

        string result = await root(new Ping("hi"), provider, CancellationToken.None);

        result.ShouldBe("hi:stopped");
    }

    [Fact]
    public void Given_A_Value_Stage_That_Throws_Synchronously_When_Entered_Then_The_Exception_Propagates()
    {
        var failure = new InvalidOperationException("boom");
        ValueLevelEntry<string> root = ValueChainBuilder.Typed<Ping, string>(
            new StageChain([typeof(SynchronouslyThrowingStage)], []));

        var exception = Should.Throw<InvalidOperationException>(() =>
            root(new Ping("hi"), Provider(new SynchronouslyThrowingStage(failure)), CancellationToken.None));

        exception.ShouldBeSameAs(failure);
    }

    [Fact]
    public void Given_A_Chain_Without_The_Stage_Service_When_Entered_Then_DI_Failure_Propagates()
    {
        ValueLevelEntry<string> root = ValueChainBuilder.Typed<Ping, string>(
            new StageChain([typeof(ForwardingTypedStage)], []));

        Should.Throw<InvalidOperationException>(() =>
            root(new Ping("hi"), Provider(new PingHandler()), CancellationToken.None));
    }

    [Fact]
    public async Task Given_A_Faulted_Value_Stage_When_Entered_Then_The_Failure_Propagates_When_Awaited()
    {
        var failure = new InvalidOperationException("boom");
        ValueLevelEntry<string> root = ValueChainBuilder.Typed<Ping, string>(
            new StageChain([typeof(FaultedStage)], []));

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await root(new Ping("hi"), Provider(new FaultedStage(failure)), CancellationToken.None));

        exception.ShouldBeSameAs(failure);
    }

    [Fact]
    public async Task Given_A_Canceled_Value_Stage_When_Entered_Then_Cancellation_Propagates_When_Awaited()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        ValueLevelEntry<string> root = ValueChainBuilder.Typed<Ping, string>(
            new StageChain([typeof(CanceledStage)], []));

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await root(new Ping("hi"), Provider(new CanceledStage(source.Token)), CancellationToken.None));

        exception.CancellationToken.ShouldBe(source.Token);
    }

    [Fact]
    public async Task Given_A_Value_Stage_For_A_Base_Request_When_Entered_With_A_Derived_Request_Then_It_Runs()
    {
        List<string> log = [];
        ServiceProvider provider = Provider(new BaseCommandStage(log), new ResetHandler());
        ValueLevelEntry<string> root = ValueChainBuilder.Typed<Reset, string>(
            new StageChain([typeof(BaseCommandStage)], []));

        string result = await root(new Reset(), provider, CancellationToken.None);

        result.ShouldBe("reset");
        log.ShouldBe(["base"]);
    }

    [Fact]
    public async Task Given_A_Value_Stage_Replaces_The_Token_When_Continuing_Then_Every_Lower_Level_Receives_It()
    {
        using var originalSource = new CancellationTokenSource();
        using var replacementSource = new CancellationTokenSource();
        var lowerStage = new TokenRecordingStage();
        var handler = new TokenRecordingHandler<TokenRecordingMarker>();
        ServiceProvider provider = Provider(
            new TokenReplacingStage(replacementSource.Token), lowerStage, handler);
        ValueLevelEntry<string> root = ValueChainBuilder.Typed<Ping, string>(
            new StageChain([typeof(TokenReplacingStage), typeof(TokenRecordingStage)], []));

        await root(new Ping("hi"), provider, originalSource.Token);

        lowerStage.Token.ShouldBe(replacementSource.Token);
        handler.Token.ShouldBe(replacementSource.Token);
    }

    [Fact]
    public async Task Given_A_Value_Continuation_Invoked_Twice_When_Entered_Then_The_Lower_Level_Is_Resolved_Twice()
    {
        using ServiceProvider inner = Provider(
            new DoubleNextStage(), new InnerStage([]), new PingHandler());
        var provider = new CountingProvider(inner);
        ValueLevelEntry<string> root = ValueChainBuilder.Typed<Ping, string>(
            new StageChain([typeof(DoubleNextStage), typeof(InnerStage)], []));

        await root(new Ping("hi"), provider, CancellationToken.None);

        provider.Requested.Count(type => type == typeof(InnerStage)).ShouldBe(2);
        provider.Requested.Count(type => type == typeof(IValueRequestHandler<Ping, string>)).ShouldBe(2);
    }

    [Fact]
    public async Task Given_Overlapping_Value_Continuation_Invocations_When_Completed_Then_Each_Has_Its_Own_Result()
    {
        var handler = new OverlappingHandler<OverlappingMarker>();
        ServiceProvider provider = Provider(new OverlappingStage(), handler);
        ValueLevelEntry<string> root = ValueChainBuilder.Typed<Ping, string>(
            new StageChain([typeof(OverlappingStage)], []));

        ValueTask<string> result = root(new Ping("hi"), provider, CancellationToken.None);

        handler.Calls.ShouldBe(2);
        handler.Second.SetResult("second");
        result.IsCompleted.ShouldBeFalse();
        handler.First.SetResult("first");
        (await result).ShouldBe("first|second");
    }

    #region Helpers

    private static ServiceProvider Provider(params object[] levels)
    {
        var services = new ServiceCollection();
        foreach (object level in levels)
        {
            switch (level)
            {
                case PingHandler handler:
                    services.AddSingleton<IValueRequestHandler<Ping, string>>(handler);
                    break;
                case LogHandler handler:
                    services.AddSingleton<IValueRequestHandler<Log>>(handler);
                    break;
                case ResetHandler handler:
                    services.AddSingleton<IValueRequestHandler<Reset, string>>(handler);
                    break;
                case TokenRecordingHandler<TokenRecordingMarker> handler:
                    services.AddSingleton<IValueRequestHandler<Ping, string>>(handler);
                    break;
                case OverlappingHandler<OverlappingMarker> handler:
                    services.AddSingleton<IValueRequestHandler<Ping, string>>(handler);
                    break;
                default:
                    services.AddSingleton(level.GetType(), level);
                    break;
            }
        }

        return services.BuildServiceProvider();
    }

    public sealed record Ping(string Text) : IValueRequest<string>;

    public sealed record Log : IValueRequest;

    public abstract record Command : IValueRequest<string>;

    public sealed record Reset : Command;

    private sealed class ForwardingTypedStage : IValueRequestStage<Ping, string>
    {
        public Ping? Request { get; private set; }

        public CancellationToken Token { get; private set; }

        public ValueTask<string> HandleAsync(
            Ping request, ValueContinuation<string> next, CancellationToken cancellationToken)
        {
            Request = request;
            Token = cancellationToken;
            return next.InvokeAsync();
        }
    }

    private sealed class ForwardingPlainVoidStage : IValueRequestStage<Log>
    {
        public ValueTask HandleAsync(
            Log request, ValueContinuation next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    private sealed class BridgedTypedVoidStage(SingleConsumptionValueTaskSource<NoResult> source)
        : IValueRequestStage<Log, NoResult>
    {
        public ValueTask<NoResult> HandleAsync(
            Log request, ValueContinuation<NoResult> next, CancellationToken cancellationToken)
        {
            next.InvokeAsync().GetAwaiter().GetResult();
            return source.CreateValueTask();
        }
    }

    private sealed class OuterStage(List<string> log) : IValueRequestStage<Ping, string>
    {
        public ValueTask<string> HandleAsync(
            Ping request, ValueContinuation<string> next, CancellationToken cancellationToken)
        {
            log.Add("outer");
            return next.InvokeAsync();
        }
    }

    private sealed class InnerStage(List<string> log) : IValueRequestStage<Ping, string>
    {
        public ValueTask<string> HandleAsync(
            Ping request, ValueContinuation<string> next, CancellationToken cancellationToken)
        {
            log.Add("inner");
            return next.InvokeAsync();
        }
    }

    private sealed class TypedVoidStage(List<string> log) : IValueRequestStage<Log, NoResult>
    {
        public ValueTask<NoResult> HandleAsync(
            Log request, ValueContinuation<NoResult> next, CancellationToken cancellationToken)
        {
            log.Add("typed");
            return next.InvokeAsync();
        }
    }

    private sealed class PlainVoidStage(List<string> log) : IValueRequestStage<Log>
    {
        public ValueTask HandleAsync(
            Log request, ValueContinuation next, CancellationToken cancellationToken)
        {
            log.Add("plain");
            return next.InvokeAsync();
        }
    }

    private sealed class ShortCircuitStage : IValueRequestStage<Ping, string>
    {
        public ValueTask<string> HandleAsync(
            Ping request, ValueContinuation<string> next, CancellationToken cancellationToken)
            => new(request.Text + ":stopped");
    }

    private sealed class SynchronouslyThrowingStage(Exception failure) : IValueRequestStage<Ping, string>
    {
        public ValueTask<string> HandleAsync(
            Ping request, ValueContinuation<string> next, CancellationToken cancellationToken)
            => throw failure;
    }

    private sealed class FaultedStage(Exception failure) : IValueRequestStage<Ping, string>
    {
        public ValueTask<string> HandleAsync(
            Ping request, ValueContinuation<string> next, CancellationToken cancellationToken)
            => new(Task.FromException<string>(failure));
    }

    private sealed class CanceledStage(CancellationToken token) : IValueRequestStage<Ping, string>
    {
        public ValueTask<string> HandleAsync(
            Ping request, ValueContinuation<string> next, CancellationToken cancellationToken)
            => new(Task.FromCanceled<string>(token));
    }

    private sealed class BaseCommandStage(List<string> log) : IValueRequestStage<Command, string>
    {
        public ValueTask<string> HandleAsync(
            Command request, ValueContinuation<string> next, CancellationToken cancellationToken)
        {
            log.Add("base");
            return next.InvokeAsync();
        }
    }

    private sealed class TokenReplacingStage(CancellationToken replacement)
        : IValueRequestStage<Ping, string>
    {
        public ValueTask<string> HandleAsync(
            Ping request, ValueContinuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync(replacement);
    }

    private sealed class TokenRecordingStage : IValueRequestStage<Ping, string>
    {
        public CancellationToken Token { get; private set; }

        public ValueTask<string> HandleAsync(
            Ping request, ValueContinuation<string> next, CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            return next.InvokeAsync();
        }
    }

    private sealed class DoubleNextStage : IValueRequestStage<Ping, string>
    {
        public async ValueTask<string> HandleAsync(
            Ping request, ValueContinuation<string> next, CancellationToken cancellationToken)
        {
            await next.InvokeAsync();
            return await next.InvokeAsync();
        }
    }

    private sealed class OverlappingStage : IValueRequestStage<Ping, string>
    {
        public ValueTask<string> HandleAsync(
            Ping request, ValueContinuation<string> next, CancellationToken cancellationToken)
            => Combine(next.InvokeAsync(), next.InvokeAsync());

        private static async ValueTask<string> Combine(
            ValueTask<string> first, ValueTask<string> second)
            => $"{await first}|{await second}";
    }

    private sealed class PingHandler(List<string>? log = null) : IValueRequestHandler<Ping, string>
    {
        public ValueTask<string> HandleAsync(Ping request, CancellationToken cancellationToken)
        {
            log?.Add("handler");
            return new(request.Text);
        }
    }

    private sealed class LogHandler(List<string>? log = null) : IValueRequestHandler<Log>
    {
        public ValueTask HandleAsync(Log request, CancellationToken cancellationToken)
        {
            log?.Add("handler");
            return default;
        }
    }

    private sealed class ResetHandler : IValueRequestHandler<Reset, string>
    {
        public ValueTask<string> HandleAsync(Reset request, CancellationToken cancellationToken)
            => new("reset");
    }

    private sealed class TokenRecordingHandler<TMarker> : IValueRequestHandler<Ping, string>
    {
        public CancellationToken Token { get; private set; }

        public ValueTask<string> HandleAsync(Ping request, CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            return new(request.Text);
        }
    }

    private sealed class OverlappingHandler<TMarker> : IValueRequestHandler<Ping, string>
    {
        public TaskCompletionSource<string> First { get; } = new();

        public TaskCompletionSource<string> Second { get; } = new();

        public int Calls { get; private set; }

        public ValueTask<string> HandleAsync(Ping request, CancellationToken cancellationToken)
        {
            Calls++;
            return new ValueTask<string>((Calls == 1 ? First : Second).Task);
        }
    }

    private sealed class TokenRecordingMarker
    { }

    private sealed class OverlappingMarker
    { }

    #endregion
}
