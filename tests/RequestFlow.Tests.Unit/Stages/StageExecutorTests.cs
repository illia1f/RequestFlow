using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class StageExecutorTests
{
    [Fact]
    public async Task Given_No_Stages_When_Running_Executor_Then_Handler_Produces_Response()
    {
        var sut = new TypedStageExecutor<Ping, string>([], _pingHandler, new Ping("hi"), CancellationToken.None);

        string result = await sut.RunAsync();

        result.ShouldBe("hi:handled");
    }

    [Fact]
    public async Task Given_Two_Stages_When_Running_Executor_Then_First_Registered_Stage_Is_Outermost()
    {
        List<string> log = [];
        IRequestStage<Ping, string>[] stages = [new RecordingStage("outer", log), new RecordingStage("inner", log)];
        var sut = new TypedStageExecutor<Ping, string>(stages, _pingHandler, new Ping("hi"), CancellationToken.None);

        await sut.RunAsync();

        log.ShouldBe(["outer:enter", "inner:enter", "inner:exit", "outer:exit"]);
    }

    [Fact]
    public async Task Given_Stage_That_Awaits_Before_Calling_Next_When_Running_Executor_Then_Chain_Completes()
    {
        List<string> log = [];
        IRequestStage<Ping, string>[] stages = [new AwaitBeforeNextStage("outer", log)];
        var sut = new TypedStageExecutor<Ping, string>(stages, _pingHandler, new Ping("hi"), CancellationToken.None);

        string result = await sut.RunAsync();

        result.ShouldBe("hi:handled");
        log.ShouldBe(["outer:enter", "outer:exit"]);
    }

    [Fact]
    public async Task Given_Two_Stages_That_Await_Before_Calling_Next_When_Running_Executor_Then_First_Registered_Stage_Is_Outermost()
    {
        List<string> log = [];
        IRequestStage<Ping, string>[] stages = [new AwaitBeforeNextStage("outer", log), new AwaitBeforeNextStage("inner", log)];
        var sut = new TypedStageExecutor<Ping, string>(stages, _pingHandler, new Ping("hi"), CancellationToken.None);

        await sut.RunAsync();

        log.ShouldBe(["outer:enter", "inner:enter", "inner:exit", "outer:exit"]);
    }

    [Fact]
    public async Task Given_Stage_That_Skips_Next_When_Running_Executor_Then_Handler_Is_Not_Invoked()
    {
        IRequestStage<Ping, string>[] stages = [new ShortCircuitStage("cached")];
        var sut = new TypedStageExecutor<Ping, string>(stages, _pingHandler, new Ping("hi"), CancellationToken.None);

        string result = await sut.RunAsync();

        result.ShouldBe("cached");
        await _pingHandler.DidNotReceive().HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Given_Throwing_Handler_When_Running_Executor_Then_Exception_Propagates_Unwrapped()
    {
        _pingHandler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new InvalidTimeZoneException("no such zone")));
        IRequestStage<Ping, string>[] stages = [new RecordingStage("outer", [])];
        var sut = new TypedStageExecutor<Ping, string>(stages, _pingHandler, new Ping("hi"), CancellationToken.None);

        var exception = await Should.ThrowAsync<InvalidTimeZoneException>(() => sut.RunAsync());

        exception.Message.ShouldBe("no such zone");
    }

    [Fact]
    public async Task Given_Cancellation_Token_When_Running_Executor_Then_Stage_And_Handler_Receive_Same_Token()
    {
        using var cts = new CancellationTokenSource();
        var stage = new TokenCapturingStage();
        var sut = new TypedStageExecutor<Ping, string>([stage], _pingHandler, new Ping("hi"), cts.Token);

        await sut.RunAsync();

        stage.CapturedToken.ShouldBe(cts.Token);
        await _pingHandler.Received(1).HandleAsync(Arg.Any<Ping>(), cts.Token);
    }

    [Fact]
    public async Task Given_Void_Handler_And_One_Stage_When_Running_Executor_Then_Handler_Runs_And_Chain_Completes()
    {
        var logHandler = Substitute.For<IRequestHandler<Log>>();
        List<string> log = [];
        IRequestStage<Log, NoResult>[] stages = [new RecordingVoidStage(log)];
        var sut = new VoidStageExecutor<Log>(stages, logHandler, new Log("hi"), CancellationToken.None);

        NoResult result = await sut.RunAsync();

        result.ShouldBe(NoResult.Value);
        log.ShouldBe(["enter", "exit"]);
        await logHandler.Received(1).HandleAsync(Arg.Any<Log>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Given_Stage_That_Calls_Next_Twice_When_Running_Executor_Then_Inner_Chain_Runs_Again()
    {
        List<string> log = [];
        IRequestStage<Ping, string>[] stages = [new DoubleNextStage("outer", log), new RecordingStage("inner", log)];
        var sut = new TypedStageExecutor<Ping, string>(stages, _pingHandler, new Ping("hi"), CancellationToken.None);

        await sut.RunAsync();

        log.ShouldBe(["outer:enter", "inner:enter", "inner:exit", "inner:enter", "inner:exit", "outer:exit"]);
        await _pingHandler.Received(2).HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Given_Asynchronously_Completing_Handler_When_Stage_Calls_Next_Twice_Then_Inner_Chain_Runs_Again()
    {
        _pingHandler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>())
            .Returns(call => YieldThenReturnAsync(call.Arg<Ping>().Text + ":handled"));
        List<string> log = [];
        IRequestStage<Ping, string>[] stages = [new DoubleNextStage("outer", log), new RecordingStage("inner", log)];
        var sut = new TypedStageExecutor<Ping, string>(stages, _pingHandler, new Ping("hi"), CancellationToken.None);

        await sut.RunAsync();

        log.ShouldBe(["outer:enter", "inner:enter", "inner:exit", "inner:enter", "inner:exit", "outer:exit"]);
    }

    [Fact]
    public async Task Given_Failing_Handler_When_Outer_Stage_Retries_Then_Inner_Chain_Runs_Again()
    {
        int calls = 0;
        _pingHandler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++calls == 1
                ? Task.FromException<string>(new InvalidTimeZoneException("transient"))
                : Task.FromResult("second"));
        List<string> log = [];
        IRequestStage<Ping, string>[] stages = [new RetryOnceStage(log), new RecordingStage("inner", log)];
        var sut = new TypedStageExecutor<Ping, string>(stages, _pingHandler, new Ping("hi"), CancellationToken.None);

        string result = await sut.RunAsync();

        result.ShouldBe("second");
        log.ShouldBe(["retry:attempt", "inner:enter", "retry:attempt", "inner:enter", "inner:exit"]);
    }

    [Fact]
    public async Task Given_Stage_That_Throws_Before_Returning_A_Task_When_Outer_Stage_Retries_Then_It_Runs_Again()
    {
        var flaky = new ThrowOnFirstAttemptStage();
        IRequestStage<Ping, string>[] stages = [new RetryOnceStage([]), flaky];
        var sut = new TypedStageExecutor<Ping, string>(stages, _pingHandler, new Ping("hi"), CancellationToken.None);

        string result = await sut.RunAsync();

        result.ShouldBe("hi:handled");
        flaky.Attempts.ShouldBe(2);
    }

    [Fact]
    public async Task Given_Stage_That_Calls_Next_Again_Before_The_First_Call_Completes_When_Running_Executor_Then_Throws()
    {
        var pending = new TaskCompletionSource<string>();
        _pingHandler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        List<string> log = [];
        IRequestStage<Ping, string>[] stages = [new ConcurrentNextStage(), new RecordingStage("inner", log)];
        var sut = new TypedStageExecutor<Ping, string>(stages, _pingHandler, new Ping("hi"), CancellationToken.None);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => sut.RunAsync());

        exception.Message.ShouldContain(nameof(ConcurrentNextStage));
        exception.Message.ShouldContain("still running");
        log.ShouldBe(["inner:enter"]);
    }

    [Fact]
    public async Task Given_Stage_That_Calls_Next_From_Two_Threads_At_Once_When_Running_Executor_Then_Exactly_One_Call_Proceeds()
    {
        const int attempts = 1000;
        int handlerRuns = 0;
        int guardThrows = 0;

        // The race window depends on timing, so the test forces many synchronized collisions.
        for (int i = 0; i < attempts; i++)
        {
            var gate = new TaskCompletionSource<string>();
            var handler = Substitute.For<IRequestHandler<Ping, string>>();
            handler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    Interlocked.Increment(ref handlerRuns);
                    return gate.Task;
                });
            var stage = new SimultaneousNextStage(gate, () => Interlocked.Increment(ref guardThrows));
            var sut = new TypedStageExecutor<Ping, string>([stage], handler, new Ping("hi"), CancellationToken.None);

            await sut.RunAsync();
        }

        handlerRuns.ShouldBe(attempts);
        guardThrows.ShouldBe(attempts);
    }

    [Fact]
    public async Task Given_Void_Form_Stage_That_Calls_Next_From_Two_Threads_At_Once_When_Running_Void_Executor_Then_Exactly_One_Call_Proceeds()
    {
        const int attempts = 1000;
        int handlerRuns = 0;
        int guardThrows = 0;

        for (int i = 0; i < attempts; i++)
        {
            var gate = new TaskCompletionSource<NoResult>();
            var handler = Substitute.For<IRequestHandler<Log>>();
            handler.HandleAsync(Arg.Any<Log>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    Interlocked.Increment(ref handlerRuns);
                    return gate.Task;
                });
            object[] stages = [new SimultaneousNextVoidStage(gate, () => Interlocked.Increment(ref guardThrows))];
            var sut = new VoidStageExecutor<Log>(stages, handler, new Log("hi"), CancellationToken.None);

            await sut.RunAsync();
        }

        handlerRuns.ShouldBe(attempts);
        guardThrows.ShouldBe(attempts);
    }

    [Fact]
    public void Given_Stage_That_Returns_A_Null_Task_When_Running_Executor_Then_Throws_Naming_The_Stage()
    {
        IRequestStage<Ping, string>[] stages = [new NullTaskStage()];
        var sut = new TypedStageExecutor<Ping, string>(stages, _pingHandler, new Ping("hi"), CancellationToken.None);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => sut.RunAsync());

        exception.Message.ShouldContain(nameof(NullTaskStage));
        exception.Message.ShouldContain("null task");
    }

    [Fact]
    public void Given_Handler_That_Returns_A_Null_Task_When_Running_Executor_Then_Throws_Naming_The_Request()
    {
        var sut = new TypedStageExecutor<Nil, string>([], new NilHandler(), new Nil(), CancellationToken.None);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => sut.RunAsync());

        exception.Message.ShouldContain(nameof(Nil));
        exception.Message.ShouldContain("null task");
    }

    [Fact]
    public async Task Given_Stage_That_Returned_A_Null_Task_When_Outer_Stage_Retries_Then_It_Runs_Again()
    {
        var flaky = new NullTaskOnFirstAttemptStage();
        IRequestStage<Ping, string>[] stages = [new RetryOnceStage([]), flaky];
        var sut = new TypedStageExecutor<Ping, string>(stages, _pingHandler, new Ping("hi"), CancellationToken.None);

        string result = await sut.RunAsync();

        result.ShouldBe("hi:handled");
        flaky.Attempts.ShouldBe(2);
    }

    [Fact]
    public async Task Given_Void_Form_Stage_When_Running_Void_Executor_Then_It_Wraps_The_Handler()
    {
        var logHandler = Substitute.For<IRequestHandler<Log>>();
        List<string> log = [];
        object[] stages = [new VoidFormStage(log)];
        var sut = new VoidStageExecutor<Log>(stages, logHandler, new Log("hi"), CancellationToken.None);

        NoResult result = await sut.RunAsync();

        result.ShouldBe(NoResult.Value);
        log.ShouldBe(["void:enter", "void:exit"]);
        await logHandler.Received(1).HandleAsync(Arg.Any<Log>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Given_Void_Form_Stage_That_Awaits_Before_Calling_Next_When_Running_Void_Executor_Then_Chain_Completes()
    {
        var logHandler = Substitute.For<IRequestHandler<Log>>();
        List<string> log = [];
        object[] stages = [new AwaitBeforeNextVoidStage(log)];
        var sut = new VoidStageExecutor<Log>(stages, logHandler, new Log("hi"), CancellationToken.None);

        NoResult result = await sut.RunAsync();

        result.ShouldBe(NoResult.Value);
        log.ShouldBe(["void:enter", "void:exit"]);
        await logHandler.Received(1).HandleAsync(Arg.Any<Log>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Given_Both_Stage_Forms_When_Running_Void_Executor_Then_Array_Order_Is_Execution_Order()
    {
        var logHandler = Substitute.For<IRequestHandler<Log>>();
        List<string> log = [];
        object[] stages = [new RecordingVoidStage(log), new VoidFormStage(log)];
        var sut = new VoidStageExecutor<Log>(stages, logHandler, new Log("hi"), CancellationToken.None);

        await sut.RunAsync();

        log.ShouldBe(["enter", "void:enter", "void:exit", "exit"]);
    }

    [Fact]
    public async Task Given_Void_Form_Stage_That_Skips_Next_When_Running_Void_Executor_Then_Handler_Is_Not_Invoked()
    {
        var logHandler = Substitute.For<IRequestHandler<Log>>();
        object[] stages = [new ShortCircuitVoidStage()];
        var sut = new VoidStageExecutor<Log>(stages, logHandler, new Log("hi"), CancellationToken.None);

        await sut.RunAsync();

        await logHandler.DidNotReceive().HandleAsync(Arg.Any<Log>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Given_Void_Form_Stage_That_Returns_A_Null_Task_When_Running_Void_Executor_Then_Throws_Naming_The_Stage()
    {
        var logHandler = Substitute.For<IRequestHandler<Log>>();
        object[] stages = [new NullTaskVoidStage()];
        var sut = new VoidStageExecutor<Log>(stages, logHandler, new Log("hi"), CancellationToken.None);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => sut.RunAsync());

        exception.Message.ShouldContain(nameof(NullTaskVoidStage));
        exception.Message.ShouldContain("null task");
    }

    [Fact]
    public void Given_Void_Handler_That_Returns_A_Null_Task_When_Running_Void_Executor_Then_Throws_Naming_The_Request()
    {
        var sut = new VoidStageExecutor<Silent>([], new SilentHandler(), new Silent(), CancellationToken.None);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => sut.RunAsync());

        exception.Message.ShouldContain(nameof(Silent));
        exception.Message.ShouldContain("null task");
    }

    [Fact]
    public async Task Given_Synchronously_Completed_Task_When_Bridging_To_No_Result_Then_Returns_Cached_Task()
    {
        Task<NoResult> result = NoResultBridge.Complete(Task.CompletedTask);

        result.ShouldBeSameAs(NoResult.Task);
        await result;
    }

    #region Initialization

    private readonly IRequestHandler<Ping, string> _pingHandler;

    public StageExecutorTests()
    {
        _pingHandler = Substitute.For<IRequestHandler<Ping, string>>();
        _pingHandler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.Arg<Ping>().Text + ":handled"));
    }

    #endregion

    #region Helpers

    // Public so NSubstitute can proxy handler interfaces closed over these types.
    public sealed record Ping(string Text) : IRequest<string>;

    public sealed record Log(string Message) : IRequest;

    public sealed record Nil : IRequest<string>;

    public sealed record Silent : IRequest;

    // Other tests scan this assembly and demand a handler per request type, so each
    // fixture record needs a concrete handler even though these tests only use the mocks.
    public sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, CancellationToken cancellationToken)
            => Task.FromResult(request.Text);
    }

    public sealed class LogHandler : IRequestHandler<Log>
    {
        public Task HandleAsync(Log request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    // Breaks the handler contract on purpose, which is what the executor has to report.
    public sealed class NilHandler : IRequestHandler<Nil, string>
    {
        public Task<string> HandleAsync(Nil request, CancellationToken cancellationToken)
            => null!;
    }

    // The void form of the same broken contract, kept on its own request so the assembly scan
    // still finds exactly one handler per request.
    public sealed class SilentHandler : IRequestHandler<Silent>
    {
        public Task HandleAsync(Silent request, CancellationToken cancellationToken)
            => null!;
    }

    private sealed class RecordingStage(string name, List<string> log) : IRequestStage<Ping, string>
    {
        public async Task<string> HandleAsync(Ping request, StageDelegate<string> next, CancellationToken cancellationToken)
        {
            log.Add($"{name}:enter");
            string response = await next();
            log.Add($"{name}:exit");
            return response;
        }
    }

    private sealed class RecordingVoidStage(List<string> log) : IRequestStage<Log, NoResult>
    {
        public async Task<NoResult> HandleAsync(Log request, StageDelegate<NoResult> next, CancellationToken cancellationToken)
        {
            log.Add("enter");
            NoResult response = await next();
            log.Add("exit");
            return response;
        }
    }

    private sealed class ShortCircuitStage(string response) : IRequestStage<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, StageDelegate<string> next, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    private sealed class DoubleNextStage(string name, List<string> log) : IRequestStage<Ping, string>
    {
        public async Task<string> HandleAsync(Ping request, StageDelegate<string> next, CancellationToken cancellationToken)
        {
            log.Add($"{name}:enter");
            await next();
            string response = await next();
            log.Add($"{name}:exit");
            return response;
        }
    }

    private sealed class RetryOnceStage(List<string> log) : IRequestStage<Ping, string>
    {
        public async Task<string> HandleAsync(Ping request, StageDelegate<string> next, CancellationToken cancellationToken)
        {
            log.Add("retry:attempt");
            try
            {
                return await next();
            }
            catch (Exception)
            {
                log.Add("retry:attempt");
                return await next();
            }
        }
    }

    private sealed class ThrowOnFirstAttemptStage : IRequestStage<Ping, string>
    {
        public int Attempts { get; private set; }

        public Task<string> HandleAsync(Ping request, StageDelegate<string> next, CancellationToken cancellationToken)
        {
            Attempts++;
            return Attempts == 1 ? throw new InvalidOperationException("sync boom") : next();
        }
    }

    // Suspends on work of its own before delegating, the shape of a validation or caching stage.
    private sealed class AwaitBeforeNextStage(string name, List<string> log) : IRequestStage<Ping, string>
    {
        public async Task<string> HandleAsync(Ping request, StageDelegate<string> next, CancellationToken cancellationToken)
        {
            await Task.Yield();
            log.Add($"{name}:enter");
            string response = await next();
            log.Add($"{name}:exit");
            return response;
        }
    }

    private sealed class AwaitBeforeNextVoidStage(List<string> log) : IRequestStage<Log>
    {
        public async Task HandleAsync(Log request, StageDelegate next, CancellationToken cancellationToken)
        {
            await Task.Yield();
            log.Add("void:enter");
            await next();
            log.Add("void:exit");
        }
    }

    // Starts a second walk of the chain while the first is still suspended on the handler.
    private sealed class ConcurrentNextStage : IRequestStage<Ping, string>
    {
        public async Task<string> HandleAsync(Ping request, StageDelegate<string> next, CancellationToken cancellationToken)
        {
            Task<string> first = next();
            Task<string> second = next();

            return await first.ConfigureAwait(false) + await second.ConfigureAwait(false);
        }
    }

    // Releases both callers into next at the same instant. The gate keeps the handler's task
    // incomplete until both calls have been attempted, so an overlapping call can never look
    // like a legal sequential re-run: a second success means the guard let both through.
    private sealed class SimultaneousNextStage(TaskCompletionSource<string> gate, Action onGuardThrow)
        : IRequestStage<Ping, string>
    {
        public async Task<string> HandleAsync(Ping request, StageDelegate<string> next, CancellationToken cancellationToken)
        {
            using var barrier = new Barrier(2);
            Task<string>?[] calls = new Task<string>?[2];

            Task Caller(int slot) => Task.Run(() =>
            {
                barrier.SignalAndWait();
                try
                {
                    calls[slot] = next();
                }
                catch (InvalidOperationException)
                {
                    onGuardThrow();
                }
            });

            await Task.WhenAll(Caller(0), Caller(1));
            gate.SetResult("done");

            string result = "";
            foreach (Task<string>? call in calls)
            {
                if (call is not null)
                    result = await call;
            }

            return result;
        }
    }

    private sealed class SimultaneousNextVoidStage(TaskCompletionSource<NoResult> gate, Action onGuardThrow)
        : IRequestStage<Log>
    {
        public async Task HandleAsync(Log request, StageDelegate next, CancellationToken cancellationToken)
        {
            using var barrier = new Barrier(2);
            Task?[] calls = new Task?[2];

            Task Caller(int slot) => Task.Run(() =>
            {
                barrier.SignalAndWait();
                try
                {
                    calls[slot] = next();
                }
                catch (InvalidOperationException)
                {
                    onGuardThrow();
                }
            });

            await Task.WhenAll(Caller(0), Caller(1));
            gate.SetResult(NoResult.Value);

            foreach (Task? call in calls)
            {
                if (call is not null)
                    await call;
            }
        }
    }

    private sealed class NullTaskStage : IRequestStage<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, StageDelegate<string> next, CancellationToken cancellationToken)
            => null!;
    }

    private sealed class NullTaskOnFirstAttemptStage : IRequestStage<Ping, string>
    {
        public int Attempts { get; private set; }

        public Task<string> HandleAsync(Ping request, StageDelegate<string> next, CancellationToken cancellationToken)
        {
            Attempts++;
            return Attempts == 1 ? null! : next();
        }
    }

    private sealed class VoidFormStage(List<string> log) : IRequestStage<Log>
    {
        public async Task HandleAsync(Log request, StageDelegate next, CancellationToken cancellationToken)
        {
            log.Add("void:enter");
            await next();
            log.Add("void:exit");
        }
    }

    private sealed class ShortCircuitVoidStage : IRequestStage<Log>
    {
        public Task HandleAsync(Log request, StageDelegate next, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class NullTaskVoidStage : IRequestStage<Log>
    {
        public Task HandleAsync(Log request, StageDelegate next, CancellationToken cancellationToken)
            => null!;
    }

    private static async Task<string> YieldThenReturnAsync(string response)
    {
        await Task.Yield();
        return response;
    }

    private sealed class TokenCapturingStage : IRequestStage<Ping, string>
    {
        public CancellationToken CapturedToken { get; private set; }

        public Task<string> HandleAsync(Ping request, StageDelegate<string> next, CancellationToken cancellationToken)
        {
            CapturedToken = cancellationToken;
            return next();
        }
    }

    #endregion
}
