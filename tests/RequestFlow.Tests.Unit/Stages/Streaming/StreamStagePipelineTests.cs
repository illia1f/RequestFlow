using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Stages;

public sealed class StreamStagePipelineTests
{
    [Fact]
    public async Task Given_Two_Stream_Stages_When_Enumerating_Then_They_Run_Outermost_First()
    {
        Trace.Clear();
        IStreamDispatcher sut = Build(o =>
        {
            o.AddStreamStage<OuterStage>();
            o.AddStreamStage<InnerStage>();
        });

        await sut.Stream(new Tail(1)).CollectAsync();

        Trace.ShouldBe(["outer", "inner", "handler"]);
    }

    [Fact]
    public async Task Given_A_Transforming_Stage_When_Enumerating_Then_The_Items_Are_Transformed()
    {
        IStreamDispatcher sut = Build(o => o.AddStreamStage<DoublingStage>());

        List<int> items = await sut.Stream(new Tail(3)).CollectAsync();

        items.ShouldBe([0, 2, 4]);
    }

    [Fact]
    public async Task Given_A_Filtering_Stage_When_Enumerating_Then_The_Dropped_Items_Do_Not_Arrive()
    {
        IStreamDispatcher sut = Build(o => o.AddStreamStage<EvenOnlyStage>());

        List<int> items = await sut.Stream(new Tail(5)).CollectAsync();

        items.ShouldBe([0, 2, 4]);
    }

    [Fact]
    public async Task Given_A_Stage_That_Stops_Early_When_Enumerating_Then_The_Sequence_Ends_There()
    {
        IStreamDispatcher sut = Build(o => o.AddStreamStage<FirstTwoStage>());

        List<int> items = await sut.Stream(new Tail(10)).CollectAsync();

        items.ShouldBe([0, 1]);
    }

    [Fact]
    public async Task Given_A_Stage_That_Skips_Its_Continuation_When_Enumerating_Then_The_Handler_Does_Not_Run()
    {
        TailHandler.Started = 0;
        IStreamDispatcher sut = Build(o => o.AddStreamStage<ShortCircuitStage>());

        List<int> items = await sut.Stream(new Tail(3)).CollectAsync();

        items.ShouldBe([99]);
        TailHandler.Started.ShouldBe(0);
    }

    [Fact]
    public async Task Given_A_Stage_Invoking_Its_Continuation_Twice_When_Enumerating_Then_The_Chain_Below_Runs_Twice()
    {
        TailHandler.Started = 0;
        IStreamDispatcher sut = Build(o => o.AddStreamStage<TwiceStage>());

        List<int> items = await sut.Stream(new Tail(2)).CollectAsync();

        items.ShouldBe([0, 1, 0, 1]);
        TailHandler.Started.ShouldBe(2);
    }

    // Invoke enters the level below on the call, so dropping the sequence it returns does not undo
    // the handler resolution and the Handle call it already made.
    [Fact]
    public async Task Given_A_Stage_That_Invokes_Its_Continuation_And_Drops_It_When_Enumerating_Then_The_Handler_Below_Ran()
    {
        TailHandler.Started = 0;
        IStreamDispatcher sut = Build(o => o.AddStreamStage<DiscardingStage>());

        List<int> items = await sut.Stream(new Tail(3)).CollectAsync();

        items.ShouldBe([42]);
        TailHandler.Started.ShouldBe(1);
    }

    [Fact]
    public async Task Given_A_Stage_Returning_A_Null_Sequence_When_Enumerating_Then_Throws_Stage_Null_Stream_Exception()
    {
        IStreamDispatcher sut = Build(o => o.AddStreamStage<NullReturningStage>());

        StageNullStreamException exception = await Should.ThrowAsync<StageNullStreamException>(
            () => sut.Stream(new Tail(1)).CollectAsync());

        exception.StageType.ShouldBe(typeof(NullReturningStage));
    }

    [Fact]
    public async Task Given_A_Stage_That_Throws_When_Enumerating_Then_The_Exception_Reaches_The_Caller_Unwrapped()
    {
        IStreamDispatcher sut = Build(o => o.AddStreamStage<ThrowingStage>());

        InvalidTimeZoneException exception = await Should.ThrowAsync<InvalidTimeZoneException>(
            () => sut.Stream(new Tail(3)).CollectAsync());

        exception.Message.ShouldBe("from stage");
    }

    [Fact]
    public async Task Given_An_Open_Generic_Stream_Stage_When_Enumerating_Then_It_Reaches_The_Request()
    {
        Trace.Clear();
        IStreamDispatcher sut = Build(o => o.AddStreamStage(typeof(OpenStage<,>)));

        await sut.Stream(new Tail(1)).CollectAsync();

        Trace.ShouldContain("open");
    }

    [Fact]
    public async Task Given_A_Closed_Stream_Stage_For_A_Base_Request_When_Streaming_A_Derived_Request_Then_The_Stage_Runs()
    {
        Trace.Clear();
        IStreamDispatcher sut = Build(o => o.AddStreamStage<PulseStage>());

        List<int> items = await sut.Stream(new FastPulse()).CollectAsync();

        items.ShouldBe([7]);
        Trace.ShouldBe(["pulse"]);
    }

    #region Helpers

    private static readonly List<string> Trace = [];

    private static IStreamDispatcher Build(Action<RequestFlowOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StreamStagePipelineTests>();
            configure(o);
        });

        return services.BuildServiceProvider().CreateScope().ServiceProvider
            .GetRequiredService<IStreamDispatcher>();
    }

    public sealed record Tail(int Count) : IStreamRequest<int>;

    public abstract record Pulse : IStreamRequest<int>;

    public sealed record FastPulse : Pulse;

    // Handle itself is not an iterator, so Started moves when the chain calls the handler rather than
    // when the sequence is first enumerated. That is what lets the short-circuit test tell a handler
    // that was never called apart from one whose items were never read.
    public sealed class TailHandler : IStreamRequestHandler<Tail, int>
    {
        public static int Started;

        public IAsyncEnumerable<int> Handle(Tail request, CancellationToken cancellationToken)
        {
            Started++;
            Trace.Add("handler");

            return Iterate(request, cancellationToken);
        }

        private static async IAsyncEnumerable<int> Iterate(
            Tail request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (int i = 0; i < request.Count; i++)
            {
                await Task.Yield();
                yield return i;
            }
        }
    }

    public sealed class FastPulseHandler : IStreamRequestHandler<FastPulse, int>
    {
        public async IAsyncEnumerable<int> Handle(
            FastPulse request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return 7;
        }
    }

    public sealed class OuterStage : IStreamRequestStage<Tail, int>
    {
        public IAsyncEnumerable<int> Handle(
            Tail request, StreamContinuation<int> next, CancellationToken cancellationToken)
        {
            Trace.Add("outer");
            return next.Invoke(cancellationToken);
        }
    }

    public sealed class InnerStage : IStreamRequestStage<Tail, int>
    {
        public IAsyncEnumerable<int> Handle(
            Tail request, StreamContinuation<int> next, CancellationToken cancellationToken)
        {
            Trace.Add("inner");
            return next.Invoke(cancellationToken);
        }
    }

    public sealed class DoublingStage : IStreamRequestStage<Tail, int>
    {
        public async IAsyncEnumerable<int> Handle(
            Tail request, StreamContinuation<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (int item in next.Invoke(cancellationToken))
            {
                yield return item * 2;
            }
        }
    }

    public sealed class EvenOnlyStage : IStreamRequestStage<Tail, int>
    {
        public async IAsyncEnumerable<int> Handle(
            Tail request, StreamContinuation<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (int item in next.Invoke(cancellationToken))
            {
                if (item % 2 == 0)
                    yield return item;
            }
        }
    }

    public sealed class FirstTwoStage : IStreamRequestStage<Tail, int>
    {
        public async IAsyncEnumerable<int> Handle(
            Tail request, StreamContinuation<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            int taken = 0;
            await foreach (int item in next.Invoke(cancellationToken))
            {
                if (taken++ == 2)
                    yield break;

                yield return item;
            }
        }
    }

    public sealed class ShortCircuitStage : IStreamRequestStage<Tail, int>
    {
        public async IAsyncEnumerable<int> Handle(
            Tail request, StreamContinuation<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return 99;
        }
    }

    public sealed class TwiceStage : IStreamRequestStage<Tail, int>
    {
        public async IAsyncEnumerable<int> Handle(
            Tail request, StreamContinuation<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (int item in next.Invoke(cancellationToken))
            {
                yield return item;
            }

            await foreach (int item in next.Invoke(cancellationToken))
            {
                yield return item;
            }
        }
    }

    // Invokes its continuation and never enumerates what comes back, which is what tells an entered
    // level apart from an enumerated one.
    public sealed class DiscardingStage : IStreamRequestStage<Tail, int>
    {
        public async IAsyncEnumerable<int> Handle(
            Tail request, StreamContinuation<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _ = next.Invoke(cancellationToken);

            await Task.Yield();
            yield return 42;
        }
    }

    public sealed class NullReturningStage : IStreamRequestStage<Tail, int>
    {
        public IAsyncEnumerable<int> Handle(
            Tail request, StreamContinuation<int> next, CancellationToken cancellationToken)
            => null!;
    }

    public sealed class ThrowingStage : IStreamRequestStage<Tail, int>
    {
        public IAsyncEnumerable<int> Handle(
            Tail request, StreamContinuation<int> next, CancellationToken cancellationToken)
            => throw new InvalidTimeZoneException("from stage");
    }

    // Declared for the base request, so contravariance on TRequest is the only thing that can reach
    // FastPulse.
    public sealed class PulseStage : IStreamRequestStage<Pulse, int>
    {
        public IAsyncEnumerable<int> Handle(
            Pulse request, StreamContinuation<int> next, CancellationToken cancellationToken)
        {
            Trace.Add("pulse");
            return next.Invoke(cancellationToken);
        }
    }

    public sealed class OpenStage<TRequest, TItem> : IStreamRequestStage<TRequest, TItem>
        where TRequest : IStreamRequest<TItem>
    {
        public IAsyncEnumerable<TItem> Handle(
            TRequest request, StreamContinuation<TItem> next, CancellationToken cancellationToken)
        {
            Trace.Add("open");
            return next.Invoke(cancellationToken);
        }
    }

    #endregion
}
