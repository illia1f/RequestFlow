using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

/// <summary>
/// What one level of a chain does when it is entered, with no dispatcher in the way.
/// </summary>
public sealed class LevelTests
{
    [Fact]
    public async Task Given_A_Typed_Stage_Level_When_Entering_It_Then_The_Stage_Receives_The_Request()
    {
        var below = new RecordingBelow("below");
        LevelEntry<string> sut = TypedStage(typeof(RecordingStage), below.EnterAsync);
        using var cts = new CancellationTokenSource();

        string result = await sut(new Ping("hi"), Provider(new RecordingStage()), cts.Token);

        result.ShouldBe("hi:below");
        below.Token.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task Given_A_Void_Form_Stage_Level_When_Entering_It_Then_Its_Plain_Task_Completes_With_No_Result()
    {
        List<string> log = [];
        LevelEntry<NoResult> sut = VoidStage(typeof(VoidShapeStage), VoidBelow);

        NoResult result = await sut(new Log(), Provider(new VoidShapeStage(log)), CancellationToken.None);

        result.ShouldBe(NoResult.Value);
        log.ShouldBe(["void"]);
    }

    [Fact]
    public async Task Given_A_Typed_Stage_That_Returns_Null_When_Entering_It_Then_Throws_Naming_The_Stage()
    {
        LevelEntry<string> sut = TypedStage(typeof(NullTaskStage), NullBelow);

        StageNullTaskException exception = await Should.ThrowAsync<StageNullTaskException>(
            () => sut(new Ping("hi"), Provider(new NullTaskStage()), CancellationToken.None));

        exception.StageType.ShouldBe(typeof(NullTaskStage));
    }

    [Fact]
    public async Task Given_A_Void_Form_Stage_That_Returns_Null_When_Entering_It_Then_Throws_Naming_The_Stage()
    {
        LevelEntry<NoResult> sut = VoidStage(typeof(NullTaskVoidStage), VoidBelow);

        StageNullTaskException exception = await Should.ThrowAsync<StageNullTaskException>(
            () => sut(new Log(), Provider(new NullTaskVoidStage()), CancellationToken.None));

        exception.StageType.ShouldBe(typeof(NullTaskVoidStage));
    }

    [Fact]
    public async Task Given_A_Typed_Handler_Level_When_Entering_It_Then_It_Produces_The_Response()
    {
        LevelEntry<string> sut = LevelFactory.Handler<Ping, string>();

        string result = await sut(new Ping("hi"), Provider(new PingHandler()), CancellationToken.None);

        result.ShouldBe("hi");
    }

    [Fact]
    public async Task Given_A_Void_Handler_Level_When_Entering_It_Then_It_Completes_With_No_Result()
    {
        LevelEntry<NoResult> sut = LevelFactory.VoidHandler<Log>();

        NoResult result = await sut(new Log(), Provider(new LogHandler()), CancellationToken.None);

        result.ShouldBe(NoResult.Value);
    }

    [Fact]
    public async Task Given_A_Handler_That_Returns_Null_When_Entering_Its_Level_Then_Throws_Naming_The_Request()
    {
        LevelEntry<string> sut = LevelFactory.Handler<Broken, string>();

        HandlerNullTaskException exception = await Should.ThrowAsync<HandlerNullTaskException>(
            () => sut(new Broken(), Provider(new BrokenHandler()), CancellationToken.None));

        exception.RequestType.ShouldBe(typeof(Broken));
    }

    [Fact]
    public async Task Given_A_Request_Of_Another_Type_When_Entering_A_Stage_Level_Then_Throws_Invalid_Cast_Exception()
    {
        LevelEntry<string> sut = TypedStage(typeof(RecordingStage), NullBelow);

        await Should.ThrowAsync<InvalidCastException>(
            () => sut(new Log(), Provider(new RecordingStage()), CancellationToken.None));
    }

    // A whole chain, not one level: which level a position gets depends on the position and the
    // stage shape.
    [Fact]
    public async Task Given_A_Two_Stage_Chain_When_Entering_It_Then_The_First_Stage_Is_Outermost()
    {
        List<string> log = [];
        ServiceProvider provider = Provider(new OuterStage(log), new InnerStage(log), new PingHandler());
        LevelEntry<string> root = ChainBuilder.Typed<Ping, string>(
            Chain([typeof(OuterStage), typeof(InnerStage)], []));

        string result = await root(new Ping("hi"), provider, CancellationToken.None);

        result.ShouldBe("hi");
        log.ShouldBe(["outer", "inner"]);
    }

    [Fact]
    public async Task Given_A_Void_Chain_Of_Both_Stage_Shapes_When_Entering_It_Then_Both_Shapes_Run()
    {
        List<string> log = [];
        ServiceProvider provider = Provider(
            new TypedShapeVoidStage(log), new VoidShapeStage(log), new LogHandler());
        LevelEntry<NoResult> root = ChainBuilder.Void<Log>(
            Chain([typeof(TypedShapeVoidStage), typeof(VoidShapeStage)], [true, false]));

        await root(new Log(), provider, CancellationToken.None);

        log.ShouldBe(["typed", "void"]);
    }

    [Fact]
    public async Task Given_A_Stage_That_Short_Circuits_When_Entering_The_Chain_Then_The_Handler_Is_Never_Resolved()
    {
        ServiceProvider provider = Provider(new ShortCircuitStage());
        LevelEntry<string> root = ChainBuilder.Typed<Ping, string>(Chain([typeof(ShortCircuitStage)], []));

        string result = await root(new Ping("hi"), provider, CancellationToken.None);

        result.ShouldBe("hi:stopped");
    }

    // A stage declared for a base request reaches a derived one through the in TRequest variance
    // without implementing the closed interface, so a level has to cast to the contract.
    [Fact]
    public async Task Given_A_Stage_Declared_For_A_Base_Request_When_Entering_A_Derived_Chain_Then_It_Runs()
    {
        List<string> log = [];
        ServiceProvider provider = Provider(new BaseCommandStage(log), new ResetHandler());
        LevelEntry<string> root = ChainBuilder.Typed<Reset, string>(Chain([typeof(BaseCommandStage)], []));

        string result = await root(new Reset(), provider, CancellationToken.None);

        result.ShouldBe("reset");
        log.ShouldBe(["base"]);
    }

    [Fact]
    public async Task Given_Two_Stage_Types_When_Building_Levels_Then_Each_Reaches_Its_Own_Stage()
    {
        LevelEntry<string> forwarding = LevelFactory.Stage<Ping, string>(
            typeof(RecordingStage), new RecordingBelow("below").EnterAsync);
        LevelEntry<string> stopping = LevelFactory.Stage<Ping, string>(typeof(ShortCircuitStage), NullBelow);
        ServiceProvider provider = Provider(new RecordingStage(), new ShortCircuitStage());

        string forwarded = await forwarding(new Ping("hi"), provider, CancellationToken.None);
        string stopped = await stopping(new Ping("hi"), provider, CancellationToken.None);

        forwarded.ShouldBe("hi:below");
        stopped.ShouldBe("hi:stopped");
    }

    #region Helpers

    private static LevelEntry<string> TypedStage(Type stageType, LevelEntry<string> below)
        => LevelFactory.Stage<Ping, string>(stageType, below);

    private static LevelEntry<NoResult> VoidStage(Type stageType, LevelEntry<NoResult> below)
        => LevelFactory.VoidStage<Log>(stageType, below);

    private static StageChain Chain(Type[] stageTypes, bool[] typedShapes)
        => new(stageTypes, typedShapes);

    private static Task<string> NullBelow(
        object request, IServiceProvider services, CancellationToken cancellationToken)
        => Task.FromResult("unreached");

    private static Task<NoResult> VoidBelow(
        object request, IServiceProvider services, CancellationToken cancellationToken)
        => NoResult.Task;

    // Every level is registered under the type it will be asked for, handlers under their contract.
    private static ServiceProvider Provider(params object[] levels)
    {
        var services = new ServiceCollection();
        foreach (object level in levels)
        {
            switch (level)
            {
                case PingHandler handler:
                    services.AddSingleton<IRequestHandler<Ping, string>>(handler);
                    break;
                case LogHandler handler:
                    services.AddSingleton<IRequestHandler<Log>>(handler);
                    break;
                case ResetHandler handler:
                    services.AddSingleton<IRequestHandler<Reset, string>>(handler);
                    break;
                case BrokenHandler handler:
                    services.AddSingleton<IRequestHandler<Broken, string>>(handler);
                    break;
                default:
                    services.AddSingleton(level.GetType(), level);
                    break;
            }
        }

        return services.BuildServiceProvider();
    }

    private sealed class RecordingBelow(string value)
    {
        internal CancellationToken Token;

        public Task<string> EnterAsync(
            object request, IServiceProvider services, CancellationToken cancellationToken)
        {
            Token = cancellationToken;

            return Task.FromResult(value);
        }
    }

    public sealed record Ping(string Text) : IRequest<string>;

    public sealed record Log : IRequest;

    public sealed record Broken : IRequest<string>;

    public record Command : IRequest<string>;

    public sealed record Reset : Command;

    // The assembly scan demands one handler per request type, even for requests nothing here
    // dispatches.
    private sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, CancellationToken cancellationToken)
            => Task.FromResult(request.Text);
    }

    private sealed class LogHandler : IRequestHandler<Log>
    {
        public Task HandleAsync(Log request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class BrokenHandler : IRequestHandler<Broken, string>
    {
        public Task<string> HandleAsync(Broken request, CancellationToken cancellationToken)
            => null!;
    }

    private sealed class CommandHandler : IRequestHandler<Command, string>
    {
        public Task<string> HandleAsync(Command request, CancellationToken cancellationToken)
            => Task.FromResult("command");
    }

    private sealed class ResetHandler : IRequestHandler<Reset, string>
    {
        public Task<string> HandleAsync(Reset request, CancellationToken cancellationToken)
            => Task.FromResult("reset");
    }

    private sealed class RecordingStage : IRequestStage<Ping, string>
    {
        public async Task<string> HandleAsync(
            Ping request, Continuation<string> next, CancellationToken cancellationToken)
            => request.Text + ":" + await next.InvokeAsync(cancellationToken);
    }

    private sealed class ShortCircuitStage : IRequestStage<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, Continuation<string> next, CancellationToken cancellationToken)
            => Task.FromResult(request.Text + ":stopped");
    }

    private sealed class NullTaskStage : IRequestStage<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, Continuation<string> next, CancellationToken cancellationToken)
            => null!;
    }

    private sealed class OuterStage(List<string> log) : IRequestStage<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, Continuation<string> next, CancellationToken cancellationToken)
        {
            log.Add("outer");

            return next.InvokeAsync(cancellationToken);
        }
    }

    private sealed class InnerStage(List<string> log) : IRequestStage<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, Continuation<string> next, CancellationToken cancellationToken)
        {
            log.Add("inner");

            return next.InvokeAsync(cancellationToken);
        }
    }

    private sealed class TypedShapeVoidStage(List<string> log) : IRequestStage<Log, NoResult>
    {
        public Task<NoResult> HandleAsync(Log request, Continuation<NoResult> next, CancellationToken cancellationToken)
        {
            log.Add("typed");

            return next.InvokeAsync(cancellationToken);
        }
    }

    private sealed class VoidShapeStage(List<string> log) : IRequestStage<Log>
    {
        public Task HandleAsync(Log request, Continuation next, CancellationToken cancellationToken)
        {
            log.Add("void");

            return next.InvokeAsync(cancellationToken);
        }
    }

    private sealed class NullTaskVoidStage : IRequestStage<Log>
    {
        public Task HandleAsync(Log request, Continuation next, CancellationToken cancellationToken)
            => null!;
    }

    private sealed class BaseCommandStage(List<string> log) : IRequestStage<Command, string>
    {
        public Task<string> HandleAsync(
            Command request, Continuation<string> next, CancellationToken cancellationToken)
        {
            log.Add("base");

            return next.InvokeAsync(cancellationToken);
        }
    }

    #endregion
}
