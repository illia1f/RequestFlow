using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Stages;

public sealed class StreamChainAllocationTests
{
    // A pass-through stage that returns next.Invoke(...) directly is not an iterator, so it has no
    // state machine and no enumerator of its own. ChainAllocationTests already showed a singleton
    // resolution costs nothing, so there is nothing left to allocate here.
    [Fact]
    public async Task Given_A_Non_Iterator_Stream_Stage_When_Enumerating_Then_It_Allocates_No_More_Than_A_Stageless_One()
    {
        IStreamDispatcher staged = Build(o => o.AddStreamStage(typeof(PassThroughStage), s => s.AsSingleton()));
        IStreamDispatcher plain = Build();

        long stagedBytes = await MeasureAsync(staged);
        long plainBytes = await MeasureAsync(plain);

        stagedBytes.ShouldBe(plainBytes);
    }

    // An async-iterator stage is a real state machine and a real enumerator, unlike the pass-through
    // above, so this is where a stream chain's cost shows up.
    [Fact]
    public async Task Given_A_One_Stage_Async_Iterator_Stream_Chain_When_Enumerating_Then_It_Allocates_More_Than_A_Stageless_One()
    {
        IStreamDispatcher staged = Build(o => o.AddStreamStage(typeof(IteratingStage), s => s.AsSingleton()));
        IStreamDispatcher plain = Build();

        long stagedBytes = await MeasureAsync(staged);
        long plainBytes = await MeasureAsync(plain);

        stagedBytes.ShouldBeGreaterThan(plainBytes);
    }

    // The cost is per level rather than compounding, so a second async-iterator stage should not cost
    // more than the first one did.
    [Fact]
    public async Task Given_A_Second_Async_Iterator_Stream_Stage_When_Enumerating_Then_It_Costs_No_More_Than_The_First()
    {
        IStreamDispatcher plain = Build();
        IStreamDispatcher one = Build(o => o.AddStreamStage(typeof(IteratingStage), s => s.AsSingleton()));
        IStreamDispatcher two = Build(o =>
        {
            o.AddStreamStage(typeof(IteratingStage), s => s.AsSingleton());
            o.AddStreamStage(typeof(SecondIteratingStage), s => s.AsSingleton());
        });

        long firstLevel = await MeasureAsync(one) - await MeasureAsync(plain);
        long secondLevel = await MeasureAsync(two) - await MeasureAsync(one);

        firstLevel.ShouldBeGreaterThan(0);

        // A second level that costs nothing is the regression this test names, since a stage that
        // never joined the chain also costs nothing.
        secondLevel.ShouldBeGreaterThan(0);
        secondLevel.ShouldBeLessThanOrEqualTo(firstLevel);
    }

    // The state machine and the enumerator are built once per enumeration, not once per item: walking
    // more items through the same chain costs nothing extra.
    [Fact]
    public async Task Given_An_Async_Iterator_Stream_Chain_When_Enumerating_More_Items_Then_The_Cost_Does_Not_Grow_With_Them()
    {
        IStreamDispatcher staged = Build(o => o.AddStreamStage(typeof(IteratingStage), s => s.AsSingleton()));

        long shortWalk = await MeasureAsync(staged, items: 4);
        long longWalk = await MeasureAsync(staged, items: 64);

        longWalk.ShouldBe(shortWalk);
    }

    #region Helpers

    private static IStreamDispatcher Build(Action<RequestFlowOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StreamChainAllocationTests>();
            configure?.Invoke(o);
        });

        return services.BuildServiceProvider().CreateScope().ServiceProvider
            .GetRequiredService<IStreamDispatcher>();
    }

    // GC.GetAllocatedBytesForCurrentThread counts this thread only, so the handler completes
    // synchronously and every level stays on the calling thread.
    private static async Task<long> MeasureAsync(IStreamDispatcher dispatcher, int items = 8)
    {
        for (int i = 0; i < 64; i++)
        {
            await DrainAsync(dispatcher, items);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        await DrainAsync(dispatcher, items);

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static async Task DrainAsync(IStreamDispatcher dispatcher, int items)
    {
        await foreach (int item in dispatcher.Stream(new Ticks(items)))
        {
        }
    }

    public sealed record Ticks(int Count) : IStreamRequest<int>;

    public sealed class TicksHandler : IStreamRequestHandler<Ticks, int>
    {
        // The one await completes synchronously, so the whole walk finishes on the calling thread
        // where GC.GetAllocatedBytesForCurrentThread can see it. A real suspension would move the
        // rest to a pool thread and the measurement would mean nothing.
        public async IAsyncEnumerable<int> Handle(
            Ticks request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            for (int i = 0; i < request.Count; i++)
            {
                yield return i;
            }
        }
    }

    // Returns next.Invoke(...) directly rather than iterating it, so this is not a state machine and
    // has no enumerator of its own.
    public sealed class PassThroughStage : IStreamRequestStage<Ticks, int>
    {
        public IAsyncEnumerable<int> Handle(
            Ticks request, StreamContinuation<int> next, CancellationToken cancellationToken)
            => next.Invoke(cancellationToken);
    }

    // The shape a real transforming or filtering stage takes: an async iterator that walks its
    // continuation with await foreach and yields its own items. Unlike PassThroughStage above, this
    // has a state machine and an enumerator of its own.
    public sealed class IteratingStage : IStreamRequestStage<Ticks, int>
    {
        public async IAsyncEnumerable<int> Handle(
            Ticks request, StreamContinuation<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (int item in next.Invoke(cancellationToken))
            {
                yield return item;
            }
        }
    }

    public sealed class SecondIteratingStage : IStreamRequestStage<Ticks, int>
    {
        public async IAsyncEnumerable<int> Handle(
            Ticks request, StreamContinuation<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (int item in next.Invoke(cancellationToken))
            {
                yield return item;
            }
        }
    }

    #endregion
}
