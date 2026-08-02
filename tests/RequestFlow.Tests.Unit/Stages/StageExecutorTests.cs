using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class StageExecutorTests
{
    [Fact]
    public async Task Given_No_Stages_When_Running_Executor_Then_Handler_Produces_Response()
    {
        var sut = PingExecutor();

        string result = await sut.RunAsync();

        result.ShouldBe("hi:handled");
    }

    [Fact]
    public async Task Given_Two_Stages_When_Running_Executor_Then_First_Registered_Stage_Is_Outermost()
    {
        List<string> log = [];
        object[] stages = [new RecordingStage<Outer>("outer", log), new RecordingStage<Inner>("inner", log)];
        var sut = PingExecutor(stages);

        await sut.RunAsync();

        log.ShouldBe(["outer:enter", "inner:enter", "inner:exit", "outer:exit"]);
    }

    [Fact]
    public async Task Given_Stage_That_Awaits_Before_Calling_Next_When_Running_Executor_Then_Chain_Completes()
    {
        List<string> log = [];
        object[] stages = [new AwaitBeforeNextStage<Outer>("outer", log)];
        var sut = PingExecutor(stages);

        string result = await sut.RunAsync();

        result.ShouldBe("hi:handled");
        log.ShouldBe(["outer:enter", "outer:exit"]);
    }

    [Fact]
    public async Task Given_Two_Stages_That_Await_Before_Calling_Next_When_Running_Executor_Then_First_Registered_Stage_Is_Outermost()
    {
        List<string> log = [];
        object[] stages = [new AwaitBeforeNextStage<Outer>("outer", log), new AwaitBeforeNextStage<Inner>("inner", log)];
        var sut = PingExecutor(stages);

        await sut.RunAsync();

        log.ShouldBe(["outer:enter", "inner:enter", "inner:exit", "outer:exit"]);
    }

    [Fact]
    public async Task Given_Stage_That_Skips_Next_When_Running_Executor_Then_Handler_Is_Not_Invoked()
    {
        object[] stages = [new ShortCircuitStage("cached")];
        var sut = PingExecutor(stages);

        string result = await sut.RunAsync();

        result.ShouldBe("cached");
        await _pingHandler.DidNotReceive().HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Given_Throwing_Handler_When_Running_Executor_Then_Exception_Propagates_Unwrapped()
    {
        _pingHandler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new InvalidTimeZoneException("no such zone")));
        object[] stages = [new RecordingStage<Outer>("outer", [])];
        var sut = PingExecutor(stages);

        var exception = await Should.ThrowAsync<InvalidTimeZoneException>(() => sut.RunAsync());

        exception.Message.ShouldBe("no such zone");
    }

    [Fact]
    public async Task Given_Cancellation_Token_When_Running_Executor_Then_Stage_And_Handler_Receive_Same_Token()
    {
        using var cts = new CancellationTokenSource();
        var stage = new TokenCapturingStage();
        object[] stages = [stage];
        var sut = new TypedStageExecutor<Ping, string>(
            StageTypes(stages), ChainProvider(_pingHandler, stages), new Ping("hi"), cts.Token);

        await sut.RunAsync();

        stage.CapturedToken.ShouldBe(cts.Token);
        await _pingHandler.Received(1).HandleAsync(Arg.Any<Ping>(), cts.Token);
    }

    [Fact]
    public async Task Given_Stage_That_Substitutes_A_Token_When_Calling_Next_Then_Handler_Receives_The_Substituted_Token()
    {
        using var entry = new CancellationTokenSource();
        using var substituted = new CancellationTokenSource();
        object[] stages = [new SubstitutingStage(substituted.Token)];
        var sut = PingExecutorWithToken(entry.Token, stages);

        await sut.RunAsync();

        await _pingHandler.Received(1).HandleAsync(Arg.Any<Ping>(), substituted.Token);
        await _pingHandler.DidNotReceive().HandleAsync(Arg.Any<Ping>(), entry.Token);
    }

    // The substitution has to survive a stage that calls next without naming a token, or the
    // level below a timeout stage would quietly put the handler back on the entry token.
    [Fact]
    public async Task Given_Outer_Stage_Substituted_A_Token_When_Inner_Stage_Omits_One_Then_Inner_Levels_Keep_The_Substituted_Token()
    {
        using var entry = new CancellationTokenSource();
        using var substituted = new CancellationTokenSource();
        var inner = new TokenCapturingStage();
        object[] stages = [new SubstitutingStage(substituted.Token), inner];
        var sut = PingExecutorWithToken(entry.Token, stages);

        await sut.RunAsync();

        inner.CapturedToken.ShouldBe(substituted.Token);
        await _pingHandler.Received(1).HandleAsync(Arg.Any<Ping>(), substituted.Token);
    }

    // None is the sentinel for an omitted token, so a stage handing next a token that happens to
    // be empty inherits instead of putting the levels below it on None.
    [Fact]
    public async Task Given_Stage_That_Passes_None_When_Calling_Next_Then_Inner_Levels_Keep_The_Token_It_Received()
    {
        using var entry = new CancellationTokenSource();
        var inner = new TokenCapturingStage();
        object[] stages = [new SubstitutingStage(CancellationToken.None), inner];
        var sut = PingExecutorWithToken(entry.Token, stages);

        await sut.RunAsync();

        inner.CapturedToken.ShouldBe(entry.Token);
        await _pingHandler.Received(1).HandleAsync(Arg.Any<Ping>(), entry.Token);
        await _pingHandler.DidNotReceive().HandleAsync(Arg.Any<Ping>(), CancellationToken.None);
    }

    [Fact]
    public async Task Given_Stage_That_Calls_Next_Twice_With_Different_Tokens_When_Running_Executor_Then_Each_Pass_Uses_Its_Own_Token()
    {
        using var first = new CancellationTokenSource();
        using var second = new CancellationTokenSource();
        var inner = new TokenRecordingStage();
        object[] stages = [new TwoTokenStage(first.Token, second.Token), inner];
        var sut = PingExecutor(stages);

        await sut.RunAsync();

        inner.CapturedTokens.ShouldBe([first.Token, second.Token]);
    }

    // Retry around timeout is the pair the guard used to make impossible: a timeout stage that
    // cancels its call and awaits it out leaves a completed task behind, so re-entry is admitted.
    [Fact]
    public async Task Given_Timeout_Stage_That_Cancels_And_Awaits_Its_Call_When_An_Outer_Stage_Retries_It_Then_The_Chain_Runs_Again()
    {
        int handlerCalls = 0;
        _pingHandler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>())
            .Returns(call => ++handlerCalls == 1
                ? WaitForCancellationAsync(call.Arg<CancellationToken>())
                : Task.FromResult("second"));
        var timeout = new CancelAndAwaitStage();
        object[] stages = [new RetryOnceStage([]), timeout];
        var sut = PingExecutor(stages);

        string result = await sut.RunAsync();

        result.ShouldBe("second");
        timeout.Attempts.ShouldBe(2);
    }

    [Fact]
    public async Task Given_Void_Form_Stage_That_Substitutes_A_Token_When_Running_Void_Executor_Then_Handler_Receives_The_Substituted_Token()
    {
        var logHandler = Substitute.For<IRequestHandler<Log>>();
        using var substituted = new CancellationTokenSource();
        object[] stages = [new SubstitutingVoidStage(substituted.Token)];
        var sut = LogExecutor(logHandler, stages);

        await sut.RunAsync();

        await logHandler.Received(1).HandleAsync(Arg.Any<Log>(), substituted.Token);
    }

    [Fact]
    public async Task Given_Void_Handler_And_One_Stage_When_Running_Executor_Then_Handler_Runs_And_Chain_Completes()
    {
        var logHandler = Substitute.For<IRequestHandler<Log>>();
        List<string> log = [];
        object[] stages = [new RecordingVoidStage(log)];
        var sut = LogExecutor(logHandler, stages);

        NoResult result = await sut.RunAsync();

        result.ShouldBe(NoResult.Value);
        log.ShouldBe(["enter", "exit"]);
        await logHandler.Received(1).HandleAsync(Arg.Any<Log>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Given_Stage_That_Calls_Next_Twice_When_Running_Executor_Then_Inner_Chain_Runs_Again()
    {
        List<string> log = [];
        object[] stages = [new DoubleNextStage("outer", log), new RecordingStage<Inner>("inner", log)];
        var sut = PingExecutor(stages);

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
        object[] stages = [new DoubleNextStage("outer", log), new RecordingStage<Inner>("inner", log)];
        var sut = PingExecutor(stages);

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
        object[] stages = [new RetryOnceStage(log), new RecordingStage<Inner>("inner", log)];
        var sut = PingExecutor(stages);

        string result = await sut.RunAsync();

        result.ShouldBe("second");
        log.ShouldBe(["retry:attempt", "inner:enter", "retry:attempt", "inner:enter", "inner:exit"]);
    }

    [Fact]
    public async Task Given_Stage_That_Throws_Before_Returning_A_Task_When_Outer_Stage_Retries_Then_It_Runs_Again()
    {
        var flaky = new ThrowOnFirstAttemptStage();
        object[] stages = [new RetryOnceStage([]), flaky];
        var sut = PingExecutor(stages);

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
        object[] stages = [new ConcurrentNextStage(), new RecordingStage<Inner>("inner", log)];
        var sut = PingExecutor(stages);

        var exception = await Should.ThrowAsync<OverlappingNextCallException>(() => sut.RunAsync());

        exception.StageType.ShouldBe(typeof(ConcurrentNextStage));
        exception.Message.ShouldContain("still running");
        log.ShouldBe(["inner:enter"]);
    }

    // The outermost level and the levels below it hold their guard state in different places, so
    // an inner stage is a separate case from the outer one rather than a repeat of it.
    [Fact]
    public async Task Given_Inner_Stage_That_Calls_Next_Again_Before_The_First_Call_Completes_When_Running_Executor_Then_Throws()
    {
        var pending = new TaskCompletionSource<string>();
        _pingHandler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        List<string> log = [];
        object[] stages = [new RecordingStage<Outer>("outer", log), new ConcurrentNextStage()];
        var sut = PingExecutor(stages);

        var exception = await Should.ThrowAsync<OverlappingNextCallException>(() => sut.RunAsync());

        exception.StageType.ShouldBe(typeof(ConcurrentNextStage));
        exception.Message.ShouldContain("still running");
        log.ShouldBe(["outer:enter"]);
    }

    // A level keeps its guard state for the whole dispatch, so a stage that walked away from a
    // call still in flight overlaps with itself when an outer retry sends it back down.
    [Fact]
    public async Task Given_Stage_That_Abandoned_A_Pending_Next_Call_When_An_Outer_Stage_Retries_It_Then_Throws()
    {
        var pending = new TaskCompletionSource<string>();
        _pingHandler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var abandoning = new AbandonPendingNextStage();
        object[] stages = [new RetryOnceStage([]), abandoning];
        var sut = PingExecutor(stages);

        var exception = await Should.ThrowAsync<OverlappingNextCallException>(() => sut.RunAsync());

        exception.StageType.ShouldBe(typeof(AbandonPendingNextStage));
        abandoning.Attempts.ShouldBe(2);
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
            var sut = PingExecutorFor(handler, stage);

            await sut.RunAsync();
        }

        handlerRuns.ShouldBe(attempts);
        guardThrows.ShouldBe(attempts);
    }

    [Fact]
    public async Task Given_Inner_Stage_That_Calls_Next_From_Two_Threads_At_Once_When_Running_Executor_Then_Exactly_One_Call_Proceeds()
    {
        const int attempts = 1000;
        int handlerRuns = 0;
        int guardThrows = 0;

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
            IRequestStage<Ping, string>[] stages =
            [
                new RecordingStage<Outer>("outer", []),
                new SimultaneousNextStage(gate, () => Interlocked.Increment(ref guardThrows)),
            ];
            var sut = PingExecutorFor(handler, stages);

            await sut.RunAsync();
        }

        handlerRuns.ShouldBe(attempts);
        guardThrows.ShouldBe(attempts);
    }

    // A void-form stage reaches the same guard state as a two-parameter one, so its second call
    // has to be admitted once the first has completed.
    [Fact]
    public async Task Given_Void_Form_Stage_That_Calls_Next_Twice_When_Running_Void_Executor_Then_Inner_Chain_Runs_Again()
    {
        var handler = Substitute.For<IRequestHandler<Log>>();
        handler.HandleAsync(Arg.Any<Log>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        List<string> log = [];
        object[] stages = [new DoubleNextVoidStage(log), new RecordingVoidStage(log)];
        var sut = LogExecutor(handler, stages);

        await sut.RunAsync();

        log.ShouldBe(["void:enter", "enter", "exit", "enter", "exit", "void:exit"]);
        await handler.Received(2).HandleAsync(Arg.Any<Log>(), Arg.Any<CancellationToken>());
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
            var sut = LogExecutor(handler, stages);

            await sut.RunAsync();
        }

        handlerRuns.ShouldBe(attempts);
        guardThrows.ShouldBe(attempts);
    }

    [Fact]
    public async Task Given_Inner_Void_Form_Stage_That_Calls_Next_From_Two_Threads_At_Once_When_Running_Void_Executor_Then_Exactly_One_Call_Proceeds()
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
            object[] stages =
            [
                new VoidFormStage([]),
                new SimultaneousNextVoidStage(gate, () => Interlocked.Increment(ref guardThrows)),
            ];
            var sut = LogExecutor(handler, stages);

            await sut.RunAsync();
        }

        handlerRuns.ShouldBe(attempts);
        guardThrows.ShouldBe(attempts);
    }

    [Fact]
    public void Given_Stage_That_Returns_A_Null_Task_When_Running_Executor_Then_Throws_Naming_The_Stage()
    {
        object[] stages = [new NullTaskStage()];
        var sut = PingExecutor(stages);

        StageNullTaskException exception = Should.Throw<StageNullTaskException>(() => sut.RunAsync());

        exception.StageType.ShouldBe(typeof(NullTaskStage));
        exception.ShouldBeAssignableTo<NullTaskException>();
    }

    [Fact]
    public void Given_Handler_That_Returns_A_Null_Task_When_Running_Executor_Then_Throws_Naming_The_Request()
    {
        var sut = new TypedStageExecutor<Nil, string>(
            [], ChainProvider<IRequestHandler<Nil, string>>(new NilHandler(), []), new Nil(), CancellationToken.None);

        HandlerNullTaskException exception = Should.Throw<HandlerNullTaskException>(() => sut.RunAsync());

        exception.RequestType.ShouldBe(typeof(Nil));
        exception.ShouldBeAssignableTo<NullTaskException>();
    }

    [Fact]
    public async Task Given_Stage_That_Returned_A_Null_Task_When_Outer_Stage_Retries_Then_It_Runs_Again()
    {
        var flaky = new NullTaskOnFirstAttemptStage();
        object[] stages = [new RetryOnceStage([]), flaky];
        var sut = PingExecutor(stages);

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
        var sut = LogExecutor(logHandler, stages);

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
        var sut = LogExecutor(logHandler, stages);

        NoResult result = await sut.RunAsync();

        result.ShouldBe(NoResult.Value);
        log.ShouldBe(["void:enter", "void:exit"]);
        await logHandler.Received(1).HandleAsync(Arg.Any<Log>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Given_Both_Stage_Forms_When_Running_Void_Executor_Then_Registration_Order_Is_Execution_Order()
    {
        var logHandler = Substitute.For<IRequestHandler<Log>>();
        List<string> log = [];
        object[] stages = [new RecordingVoidStage(log), new VoidFormStage(log)];
        var sut = LogExecutor(logHandler, stages);

        await sut.RunAsync();

        log.ShouldBe(["enter", "void:enter", "void:exit", "exit"]);
    }

    [Fact]
    public async Task Given_Void_Form_Stage_That_Skips_Next_When_Running_Void_Executor_Then_Handler_Is_Not_Invoked()
    {
        var logHandler = Substitute.For<IRequestHandler<Log>>();
        object[] stages = [new ShortCircuitVoidStage()];
        var sut = LogExecutor(logHandler, stages);

        await sut.RunAsync();

        await logHandler.DidNotReceive().HandleAsync(Arg.Any<Log>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Given_Void_Form_Stage_That_Returns_A_Null_Task_When_Running_Void_Executor_Then_Throws_Naming_The_Stage()
    {
        var logHandler = Substitute.For<IRequestHandler<Log>>();
        object[] stages = [new NullTaskVoidStage()];
        var sut = LogExecutor(logHandler, stages);

        StageNullTaskException exception = Should.Throw<StageNullTaskException>(() => sut.RunAsync());

        exception.StageType.ShouldBe(typeof(NullTaskVoidStage));
    }

    [Fact]
    public void Given_Void_Handler_That_Returns_A_Null_Task_When_Running_Void_Executor_Then_Throws_Naming_The_Request()
    {
        var sut = new VoidStageExecutor<Silent>(
            [], [], ChainProvider<IRequestHandler<Silent>>(new SilentHandler(), []), new Silent(), CancellationToken.None);

        HandlerNullTaskException exception = Should.Throw<HandlerNullTaskException>(() => sut.RunAsync());

        exception.RequestType.ShouldBe(typeof(Silent));
    }

    [Fact]
    public async Task Given_Synchronously_Completed_Task_When_Bridging_To_No_Result_Then_Returns_Cached_Task()
    {
        Task<NoResult> result = NoResultBridge.Complete(Task.CompletedTask);

        result.ShouldBeSameAs(NoResult.Task);
        await result;
    }

    // The boundary case: with no level in between, the outermost stage's next exercises the
    // executor's own guard state rather than a continuation's.
    [Fact]
    public async Task Given_One_Stage_When_Running_Executor_Then_Stage_Wraps_The_Handler()
    {
        List<string> log = [];
        var sut = PingExecutor(new RecordingStage<Outer>("only", log));

        string result = await sut.RunAsync();

        result.ShouldBe("hi:handled");
        log.ShouldBe(["only:enter", "only:exit"]);
    }

    [Fact]
    public async Task Given_One_Stage_That_Calls_Next_Twice_When_Running_Executor_Then_Handler_Runs_Again()
    {
        List<string> log = [];
        var sut = PingExecutor(new DoubleNextStage("only", log));

        await sut.RunAsync();

        log.ShouldBe(["only:enter", "only:exit"]);
        await _pingHandler.Received(2).HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Given_One_Stage_That_Calls_Next_Again_Before_The_First_Call_Completes_When_Running_Executor_Then_Throws()
    {
        var pending = new TaskCompletionSource<string>();
        _pingHandler.HandleAsync(Arg.Any<Ping>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var sut = PingExecutor(new ConcurrentNextStage());

        var exception = await Should.ThrowAsync<OverlappingNextCallException>(() => sut.RunAsync());

        exception.StageType.ShouldBe(typeof(ConcurrentNextStage));
        exception.Message.ShouldContain("still running");
    }

    [Fact]
    public void Given_One_Stage_That_Returns_A_Null_Task_When_Running_Executor_Then_Throws_Naming_The_Stage()
    {
        var sut = PingExecutor(new NullTaskStage());

        StageNullTaskException exception = Should.Throw<StageNullTaskException>(() => sut.RunAsync());

        exception.StageType.ShouldBe(typeof(NullTaskStage));
    }

    [Fact]
    public void Given_One_Stage_And_Handler_That_Returns_A_Null_Task_When_Running_Executor_Then_Throws_Naming_The_Request()
    {
        object[] stages = [new NilPassThroughStage()];
        var sut = new TypedStageExecutor<Nil, string>(
            StageTypes(stages),
            ChainProvider<IRequestHandler<Nil, string>>(new NilHandler(), stages),
            new Nil(),
            CancellationToken.None);

        HandlerNullTaskException exception = Should.Throw<HandlerNullTaskException>(() => sut.RunAsync());

        exception.RequestType.ShouldBe(typeof(Nil));
    }

    [Fact]
    public async Task Given_One_Typed_Form_Stage_When_Running_Void_Executor_Then_It_Wraps_The_Handler()
    {
        var logHandler = Substitute.For<IRequestHandler<Log>>();
        List<string> log = [];
        var sut = LogExecutor(logHandler, new RecordingVoidStage(log));

        NoResult result = await sut.RunAsync();

        result.ShouldBe(NoResult.Value);
        log.ShouldBe(["enter", "exit"]);
        await logHandler.Received(1).HandleAsync(Arg.Any<Log>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Given_One_Void_Form_Stage_That_Calls_Next_Twice_When_Running_Void_Executor_Then_Handler_Runs_Again()
    {
        var handler = Substitute.For<IRequestHandler<Log>>();
        handler.HandleAsync(Arg.Any<Log>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        List<string> log = [];
        var sut = LogExecutor(handler, new DoubleNextVoidStage(log));

        await sut.RunAsync();

        log.ShouldBe(["void:enter", "void:exit"]);
        await handler.Received(2).HandleAsync(Arg.Any<Log>(), Arg.Any<CancellationToken>());
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

    // A chain resolves each stage from DI by its registered type, so every level needs a stage
    // type of its own; the handler goes in the same provider the bottom level resolves from.
    private static ServiceProvider ChainProvider<THandler>(THandler handler, object[] stages)
        where THandler : class
    {
        var services = new ServiceCollection();
        services.AddSingleton(handler);
        foreach (object stage in stages)
        {
            services.AddSingleton(stage.GetType(), stage);
        }

        return services.BuildServiceProvider();
    }

    private static Type[] StageTypes(object[] stages)
    {
        Type[] types = new Type[stages.Length];
        for (int i = 0; i < stages.Length; i++)
        {
            types[i] = stages[i].GetType();
        }

        return types;
    }

    // The freeze settles which contract shape each void level runs under; these tests stand in
    // for it by reading the shape off the instances they were handed.
    private static bool[] TypedShapes<TRequest>(object[] stages)
        where TRequest : IRequest<NoResult>
    {
        bool[] shapes = new bool[stages.Length];
        for (int i = 0; i < stages.Length; i++)
        {
            shapes[i] = stages[i] is IRequestStage<TRequest, NoResult>;
        }

        return shapes;
    }

    private TypedStageExecutor<Ping, string> PingExecutor(params object[] stages)
        => PingExecutorFor(_pingHandler, stages);

    private static TypedStageExecutor<Ping, string> PingExecutorFor(
        IRequestHandler<Ping, string> handler, params object[] stages)
        => new(StageTypes(stages), ChainProvider(handler, stages), new Ping("hi"), CancellationToken.None);

    private TypedStageExecutor<Ping, string> PingExecutorWithToken(CancellationToken cancellationToken, object[] stages)
        => new(StageTypes(stages), ChainProvider(_pingHandler, stages), new Ping("hi"), cancellationToken);

    private static VoidStageExecutor<Log> LogExecutor(IRequestHandler<Log> handler, params object[] stages)
        => new(
            StageTypes(stages),
            TypedShapes<Log>(stages),
            ChainProvider(handler, stages),
            new Log("hi"),
            CancellationToken.None);

    // Position markers, so two levels of the same stage class are two registrable types.
    private sealed class Outer
    { }

    private sealed class Inner
    { }

    // Public so NSubstitute can proxy handler interfaces closed over these types.
    public sealed record Ping(string Text) : IRequest<string>;

    public sealed record Log(string Message) : IRequest;

    public sealed record Nil : IRequest<string>;

    public sealed record Silent : IRequest;

    // The assembly scan demands one handler per request type, even for requests only mocked here.
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

    private sealed class RecordingStage<TPosition>(string name, List<string> log) : IRequestStage<Ping, string>
    {
        public async Task<string> HandleAsync(Ping request, IContinuation<string> next, CancellationToken cancellationToken)
        {
            log.Add($"{name}:enter");
            string response = await next.InvokeAsync();
            log.Add($"{name}:exit");
            return response;
        }
    }

    private sealed class RecordingVoidStage(List<string> log) : IRequestStage<Log, NoResult>
    {
        public async Task<NoResult> HandleAsync(Log request, IContinuation<NoResult> next, CancellationToken cancellationToken)
        {
            log.Add("enter");
            NoResult response = await next.InvokeAsync();
            log.Add("exit");
            return response;
        }
    }

    private sealed class ShortCircuitStage(string response) : IRequestStage<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, IContinuation<string> next, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    private sealed class DoubleNextStage(string name, List<string> log) : IRequestStage<Ping, string>
    {
        public async Task<string> HandleAsync(Ping request, IContinuation<string> next, CancellationToken cancellationToken)
        {
            log.Add($"{name}:enter");
            await next.InvokeAsync();
            string response = await next.InvokeAsync();
            log.Add($"{name}:exit");
            return response;
        }
    }

    private sealed class DoubleNextVoidStage(List<string> log) : IRequestStage<Log>
    {
        public async Task HandleAsync(Log request, IContinuation next, CancellationToken cancellationToken)
        {
            log.Add("void:enter");
            await next.InvokeAsync();
            await next.InvokeAsync();
            log.Add("void:exit");
        }
    }

    private sealed class RetryOnceStage(List<string> log) : IRequestStage<Ping, string>
    {
        public async Task<string> HandleAsync(Ping request, IContinuation<string> next, CancellationToken cancellationToken)
        {
            log.Add("retry:attempt");
            try
            {
                return await next.InvokeAsync();
            }
            catch (Exception)
            {
                log.Add("retry:attempt");
                return await next.InvokeAsync();
            }
        }
    }

    // The timeout shape: it starts the rest of the chain, gives up on it, and leaves that call in
    // flight rather than awaiting it out.
    private sealed class AbandonPendingNextStage : IRequestStage<Ping, string>
    {
        public int Attempts { get; private set; }

        public Task<string> HandleAsync(Ping request, IContinuation<string> next, CancellationToken cancellationToken)
        {
            Attempts++;
            _ = next.InvokeAsync();

            return Task.FromException<string>(new TimeoutException("gave up"));
        }
    }

    private sealed class ThrowOnFirstAttemptStage : IRequestStage<Ping, string>
    {
        public int Attempts { get; private set; }

        public Task<string> HandleAsync(Ping request, IContinuation<string> next, CancellationToken cancellationToken)
        {
            Attempts++;
            return Attempts == 1 ? throw new InvalidOperationException("sync boom") : next.InvokeAsync();
        }
    }

    // Suspends on work of its own before delegating, the shape of a validation or caching stage.
    private sealed class AwaitBeforeNextStage<TPosition>(string name, List<string> log) : IRequestStage<Ping, string>
    {
        public async Task<string> HandleAsync(Ping request, IContinuation<string> next, CancellationToken cancellationToken)
        {
            await Task.Yield();
            log.Add($"{name}:enter");
            string response = await next.InvokeAsync();
            log.Add($"{name}:exit");
            return response;
        }
    }

    private sealed class AwaitBeforeNextVoidStage(List<string> log) : IRequestStage<Log>
    {
        public async Task HandleAsync(Log request, IContinuation next, CancellationToken cancellationToken)
        {
            await Task.Yield();
            log.Add("void:enter");
            await next.InvokeAsync();
            log.Add("void:exit");
        }
    }

    // Starts a second walk of the chain while the first is still suspended on the handler.
    private sealed class ConcurrentNextStage : IRequestStage<Ping, string>
    {
        public async Task<string> HandleAsync(Ping request, IContinuation<string> next, CancellationToken cancellationToken)
        {
            Task<string> first = next.InvokeAsync();
            Task<string> second = next.InvokeAsync();

            return await first.ConfigureAwait(false) + await second.ConfigureAwait(false);
        }
    }

    // Releases both callers into next at the same instant. The gate holds the handler's task
    // incomplete until both calls are attempted, so a second success means the guard let both
    // through rather than a legal sequential re-run.
    private sealed class SimultaneousNextStage(TaskCompletionSource<string> gate, Action onGuardThrow)
        : IRequestStage<Ping, string>
    {
        public async Task<string> HandleAsync(Ping request, IContinuation<string> next, CancellationToken cancellationToken)
        {
            using var barrier = new Barrier(2);
            Task<string>?[] calls = new Task<string>?[2];

            Task Caller(int slot) => Task.Run(() =>
            {
                barrier.SignalAndWait();
                try
                {
                    calls[slot] = next.InvokeAsync();
                }
                catch (OverlappingNextCallException)
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
        public async Task HandleAsync(Log request, IContinuation next, CancellationToken cancellationToken)
        {
            using var barrier = new Barrier(2);
            Task?[] calls = new Task?[2];

            Task Caller(int slot) => Task.Run(() =>
            {
                barrier.SignalAndWait();
                try
                {
                    calls[slot] = next.InvokeAsync();
                }
                catch (OverlappingNextCallException)
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
        public Task<string> HandleAsync(Ping request, IContinuation<string> next, CancellationToken cancellationToken)
            => null!;
    }

    // Delegates straight to next, so the null task the handler returns is the one reported.
    private sealed class NilPassThroughStage : IRequestStage<Nil, string>
    {
        public Task<string> HandleAsync(Nil request, IContinuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    private sealed class NullTaskOnFirstAttemptStage : IRequestStage<Ping, string>
    {
        public int Attempts { get; private set; }

        public Task<string> HandleAsync(Ping request, IContinuation<string> next, CancellationToken cancellationToken)
        {
            Attempts++;
            return Attempts == 1 ? null! : next.InvokeAsync();
        }
    }

    private sealed class VoidFormStage(List<string> log) : IRequestStage<Log>
    {
        public async Task HandleAsync(Log request, IContinuation next, CancellationToken cancellationToken)
        {
            log.Add("void:enter");
            await next.InvokeAsync();
            log.Add("void:exit");
        }
    }

    private sealed class ShortCircuitVoidStage : IRequestStage<Log>
    {
        public Task HandleAsync(Log request, IContinuation next, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class NullTaskVoidStage : IRequestStage<Log>
    {
        public Task HandleAsync(Log request, IContinuation next, CancellationToken cancellationToken)
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

        public Task<string> HandleAsync(Ping request, IContinuation<string> next, CancellationToken cancellationToken)
        {
            CapturedToken = cancellationToken;
            return next.InvokeAsync();
        }
    }

    // The shape a timeout stage takes: it puts its own token on the rest of the chain instead of
    // the one it was handed.
    private sealed class SubstitutingStage(CancellationToken substitute) : IRequestStage<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, IContinuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync(substitute);
    }

    private sealed class SubstitutingVoidStage(CancellationToken substitute) : IRequestStage<Log>
    {
        public Task HandleAsync(Log request, IContinuation next, CancellationToken cancellationToken)
            => next.InvokeAsync(substitute);
    }

    private sealed class TokenRecordingStage : IRequestStage<Ping, string>
    {
        public List<CancellationToken> CapturedTokens { get; } = [];

        public Task<string> HandleAsync(Ping request, IContinuation<string> next, CancellationToken cancellationToken)
        {
            CapturedTokens.Add(cancellationToken);
            return next.InvokeAsync();
        }
    }

    private sealed class TwoTokenStage(CancellationToken first, CancellationToken second) : IRequestStage<Ping, string>
    {
        public async Task<string> HandleAsync(Ping request, IContinuation<string> next, CancellationToken cancellationToken)
        {
            await next.InvokeAsync(first);

            return await next.InvokeAsync(second);
        }
    }

    // The timeout shape written to compose: it cancels the call it started and awaits it out, so
    // the level is free when an outer retry re-enters. Cancelling at once keeps the test fast.
    private sealed class CancelAndAwaitStage : IRequestStage<Ping, string>
    {
        public int Attempts { get; private set; }

        public async Task<string> HandleAsync(Ping request, IContinuation<string> next, CancellationToken cancellationToken)
        {
            Attempts++;
            if (Attempts > 1)
                return await next.InvokeAsync();

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task<string> call = next.InvokeAsync(linked.Token);
            linked.Cancel();
            try
            {
                return await call;
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("gave up");
            }
        }
    }

    // Stands in for a handler that honours its token: it finishes only once cancelled.
    private static async Task<string> WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        var cancelled = new TaskCompletionSource<string>();
        using (cancellationToken.Register(() => cancelled.TrySetCanceled(cancellationToken)))
        {
            return await cancelled.Task;
        }
    }

    #endregion
}
