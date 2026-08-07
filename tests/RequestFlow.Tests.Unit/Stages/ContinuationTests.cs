#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class ContinuationTests
{
    [Theory]
    [InlineData(typeof(Continuation<string>))]
    [InlineData(typeof(Continuation))]
    public void Given_A_Continuation_Shape_When_Inspecting_Its_Type_Then_It_Is_A_Value_Type(Type shape)
    {
        shape.IsValueType.ShouldBeTrue();
    }

    // The struct is copied into every stage call, so it has to stay small.
#if NET8_0_OR_GREATER
    [Fact]
    public void Given_A_Continuation_When_Measuring_It_Then_It_Holds_Only_Its_Three_References_And_A_Token()
    {
        Unsafe.SizeOf<Continuation<string>>().ShouldBeLessThanOrEqualTo(32);
    }
#endif

    [Fact]
    public async Task Given_A_Continuation_Over_A_Level_When_Invoking_It_Then_The_Level_Receives_The_Request_And_Token()
    {
        var level = new RecordingLevel();
        using var source = new CancellationTokenSource();
        var request = new Ping();
        var continuation = new Continuation<string>(
            level.EnterAsync, request, EmptyProvider.Instance, source.Token);

        (await continuation.InvokeAsync()).ShouldBe("entered");
        level.Request.ShouldBeSameAs(request);
        level.Token.ShouldBe(source.Token);
    }

    [Fact]
    public async Task Given_A_Continuation_When_Invoking_It_With_A_Token_Then_That_Token_Reaches_The_Level()
    {
        var level = new RecordingLevel();
        using var inherited = new CancellationTokenSource();
        using var supplied = new CancellationTokenSource();
        var continuation = new Continuation<string>(
            level.EnterAsync, new Ping(), EmptyProvider.Instance, inherited.Token);

        await continuation.InvokeAsync(supplied.Token);

        level.Token.ShouldBe(supplied.Token);
    }

    [Fact]
    public async Task Given_A_Stage_Under_Test_When_Handed_A_Continuation_Over_A_Delegate_Then_It_Runs_With_No_Container()
    {
        var stage = new AnnotatingStage();

        string result = await stage.HandleAsync(
            new Ping(), Continuation<string>.Over(_ => Task.FromResult("from the chain")), CancellationToken.None);

        result.ShouldBe("[from the chain]");
    }

    [Fact]
    public async Task Given_A_Continuation_Over_A_Delegate_When_A_Stage_Overrides_The_Token_Then_The_Delegate_Sees_Its_Token()
    {
        using var inherited = new CancellationTokenSource();
        using var replacement = new CancellationTokenSource();
        CancellationToken seen = default;

        await new TokenReplacingStage(replacement.Token).HandleAsync(
            new Ping(),
            Continuation<string>.Over(
                token =>
                {
                    seen = token;
                    return Task.FromResult("done");
                },
                inherited.Token),
            inherited.Token);

        seen.ShouldBe(replacement.Token);
    }

    // Without the second argument there would be nothing for None to fall back to.
    [Fact]
    public async Task Given_A_Continuation_Over_A_Delegate_When_A_Stage_Names_No_Token_Then_The_Delegate_Sees_The_Inherited_One()
    {
        using var inherited = new CancellationTokenSource();
        CancellationToken seen = default;

        await new AnnotatingStage().HandleAsync(
            new Ping(),
            Continuation<string>.Over(
                token =>
                {
                    seen = token;
                    return Task.FromResult("done");
                },
                inherited.Token),
            inherited.Token);

        seen.ShouldBe(inherited.Token);
    }

    [Fact]
    public async Task Given_A_Void_Stage_Under_Test_When_Handed_A_Continuation_Over_A_Delegate_Then_It_Runs_With_No_Container()
    {
        int ran = 0;
        using var supplied = new CancellationTokenSource();
        CancellationToken seen = default;

        await new VoidCountingStage().HandleAsync(
            new Note(),
            Continuation.Over(
                token =>
                {
                    ran++;
                    seen = token;
                    return Task.CompletedTask;
                },
                supplied.Token),
            supplied.Token);

        ran.ShouldBe(1);
        seen.ShouldBe(supplied.Token);
    }

    // A repeated call runs the rest of the chain again, so a continuation built by Over has to
    // allow that too.
    [Fact]
    public async Task Given_A_Continuation_Over_A_Delegate_When_A_Stage_Invokes_It_Twice_Then_The_Delegate_Runs_Twice()
    {
        int calls = 0;
        Continuation<string> next = Continuation<string>.Over(_ => Task.FromResult($"{++calls}"));

        await new DoubleInvokingStage().HandleAsync(new Ping(), next, CancellationToken.None);

        calls.ShouldBe(2);
    }

    // A real next can throw before there is a task to hand back, so the double reproduces that shape.
    [Fact]
    public void Given_A_Continuation_Over_A_Delegate_That_Throws_When_Invoking_It_Then_The_Exception_Escapes_Synchronously()
    {
        Continuation<string> next = Continuation<string>.Over(_ => throw new InvalidTimeZoneException("no task"));

        var thrown = Should.Throw<InvalidTimeZoneException>(() => { _ = next.InvokeAsync(); });

        thrown.Message.ShouldBe("no task");
    }

    [Fact]
    public void Given_A_Void_Continuation_Over_A_Delegate_That_Throws_When_Invoking_It_Then_The_Exception_Escapes_Synchronously()
    {
        Continuation next = Continuation.Over(_ => throw new InvalidTimeZoneException("no task"));

        var thrown = Should.Throw<InvalidTimeZoneException>(() => { _ = next.InvokeAsync(); });

        thrown.Message.ShouldBe("no task");
    }

    [Fact]
    public void Given_No_Delegate_When_Building_A_Continuation_Over_It_Then_It_Refuses()
    {
        Should.Throw<ArgumentNullException>(() => { _ = Continuation<string>.Over(null!); });
    }

    [Fact]
    public void Given_No_Delegate_When_Building_A_Void_Continuation_Over_It_Then_It_Refuses()
    {
        Should.Throw<ArgumentNullException>(() => { _ = Continuation.Over(null!); });
    }

    // A stage handed the default value holds no chain, so the failure has to say so rather than
    // surface as a null dereference.
    [Fact]
    public void Given_A_Default_Continuation_When_Invoking_It_Then_It_Says_It_Was_Never_Built()
    {
        Continuation<string> next = default;

        var thrown = Should.Throw<InvalidOperationException>(() => next.InvokeAsync());

        thrown.Message.ShouldContain("default");
        thrown.Message.ShouldContain("Over");
    }

    [Fact]
    public void Given_A_Default_Void_Continuation_When_Invoking_It_Then_It_Says_It_Was_Never_Built()
    {
        Continuation next = default;

        var thrown = Should.Throw<InvalidOperationException>(() => next.InvokeAsync());

        thrown.Message.ShouldContain("default");
        thrown.Message.ShouldContain("Over");
    }

    #region Helpers

    public sealed record Ping : IRequest<string>;

    // The assembly scan demands one handler per request type, even for requests only used here.
    public sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, CancellationToken cancellationToken)
            => Task.FromResult("handled");
    }

    public sealed record Note : IRequest;

    public sealed class NoteHandler : IRequestHandler<Note>
    {
        public Task HandleAsync(Note request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class AnnotatingStage : IRequestStage<Ping, string>
    {
        public async Task<string> HandleAsync(
            Ping request, Continuation<string> next, CancellationToken cancellationToken)
            => $"[{await next.InvokeAsync()}]";
    }

    public sealed class TokenReplacingStage(CancellationToken replacement) : IRequestStage<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, Continuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync(replacement);
    }

    public sealed class DoubleInvokingStage : IRequestStage<Ping, string>
    {
        public async Task<string> HandleAsync(
            Ping request, Continuation<string> next, CancellationToken cancellationToken)
        {
            await next.InvokeAsync();

            return await next.InvokeAsync();
        }
    }

    public sealed class VoidCountingStage : IRequestStage<Note>
    {
        public Task HandleAsync(Note request, Continuation next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    // A level is a delegate, so this records through a method group rather than an interface.
    private sealed class RecordingLevel
    {
        internal object? Request;
        internal CancellationToken Token;

        public Task<string> EnterAsync(
            object request, IServiceProvider services, CancellationToken cancellationToken)
        {
            Request = request;
            Token = cancellationToken;

            return Task.FromResult("entered");
        }
    }

    private sealed class EmptyProvider : IServiceProvider
    {
        internal static readonly EmptyProvider Instance = new();

        public object? GetService(Type serviceType) => null;
    }

    #endregion
}
