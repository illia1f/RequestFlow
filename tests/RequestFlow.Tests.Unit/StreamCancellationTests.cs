using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class StreamCancellationTests
{
    [Fact]
    public async Task Given_A_Cancelled_Dispatch_Token_When_Enumerating_Then_The_Walk_Stops()
    {
        using var source = new CancellationTokenSource();
        IStreamDispatcher sut = Build();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (int item in sut.Stream(new Endless(), source.Token))
            {
                source.Cancel();
            }
        });
    }

    [Fact]
    public async Task Given_A_Cancelled_Iteration_Token_When_Enumerating_Then_The_Walk_Stops()
    {
        using var source = new CancellationTokenSource();
        IStreamDispatcher sut = Build();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (int item in sut.Stream(new Endless()).WithCancellation(source.Token))
            {
                source.Cancel();
            }
        });
    }

    [Fact]
    public async Task Given_Both_Tokens_When_The_Dispatch_One_Is_Cancelled_Then_The_Walk_Stops()
    {
        using var dispatch = new CancellationTokenSource();
        using var iteration = new CancellationTokenSource();
        IStreamDispatcher sut = Build();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (int item in sut.Stream(new Endless(), dispatch.Token).WithCancellation(iteration.Token))
            {
                dispatch.Cancel();
            }
        });
    }

    [Fact]
    public async Task Given_Both_Tokens_When_The_Iteration_One_Is_Cancelled_Then_The_Walk_Stops()
    {
        using var dispatch = new CancellationTokenSource();
        using var iteration = new CancellationTokenSource();
        IStreamDispatcher sut = Build();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (int item in sut.Stream(new Endless(), dispatch.Token).WithCancellation(iteration.Token))
            {
                iteration.Cancel();
            }
        });
    }

    // The handler sees the dispatch token as an ordinary argument, so it needs no attribute to
    // observe cancellation.
    [Fact]
    public async Task Given_A_Dispatch_Token_When_Enumerating_Then_The_Handler_Receives_A_Token_It_Can_Observe()
    {
        using var source = new CancellationTokenSource();
        IStreamDispatcher sut = Build();
        WatchingHandler.Seen = default;

        await using (IAsyncEnumerator<int> enumerator = sut.Stream(new Watched(), source.Token).GetAsyncEnumerator())
        {
            await enumerator.MoveNextAsync();
        }

        WatchingHandler.Seen.CanBeCanceled.ShouldBeTrue();
        WatchingHandler.Seen.IsCancellationRequested.ShouldBeFalse();
        source.Cancel();
        WatchingHandler.Seen.IsCancellationRequested.ShouldBeTrue();
    }

    // The library hands the handler the dispatch token as an ordinary argument, so cancellation
    // works whether or not the author decorated the parameter. Both shapes are pinned because the
    // documentation tells users to decorate, and neither the rule nor the library may depend on it.
    [Fact]
    public async Task Given_An_Undecorated_Handler_When_The_Dispatch_Token_Is_Cancelled_Then_The_Walk_Stops()
    {
        using var source = new CancellationTokenSource();
        IStreamDispatcher sut = Build();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (int item in sut.Stream(new Undecorated(), source.Token))
            {
                source.Cancel();
            }
        });
    }

    // The walk joins the dispatch token with the enumeration one and hands the join down as an
    // ordinary argument, so a WithCancellation token reaches an undecorated handler too. The
    // attribute only silences CS8425.
    [Fact]
    public async Task Given_An_Undecorated_Handler_When_The_Iteration_Token_Is_Cancelled_Then_The_Walk_Stops()
    {
        using var source = new CancellationTokenSource();
        IStreamDispatcher sut = Build();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (int item in sut.Stream(new Undecorated()).WithCancellation(source.Token))
            {
                source.Cancel();
            }
        });
    }

    // A pass-through stage hands the dispatcher the sequence the levels below returned, so the
    // walk must not re-attach the dispatch token to it. The substituted token holds for the
    // handler in both stage shapes, and only the pass-through one can catch the walk doing more.
    [Fact]
    public async Task Given_A_Pass_Through_Stage_Substituting_A_Token_When_The_Dispatch_Token_Is_Cancelled_Then_The_Walk_Completes()
    {
        using var dispatch = new CancellationTokenSource();
        using var shield = new CancellationTokenSource();
        ShieldingStage.Shield = shield.Token;
        IStreamDispatcher sut = Build(o => o.AddStreamStage<ShieldingStage>());

        List<int> items = [];
        await foreach (int item in sut.Stream(new Shielded(), dispatch.Token))
        {
            items.Add(item);
            dispatch.Cancel();
        }

        items.ShouldBe([0, 1, 2, 3, 4]);
    }

    #region Helpers

    private static IStreamDispatcher Build(Action<RequestFlowOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StreamCancellationTests>();
            configure?.Invoke(o);
        });

        return services.BuildServiceProvider().CreateScope().ServiceProvider
            .GetRequiredService<IStreamDispatcher>();
    }

    public sealed record Endless : IStreamRequest<int>;

    public sealed record Watched : IStreamRequest<int>;

    public sealed record Undecorated : IStreamRequest<int>;

    public sealed record Shielded : IStreamRequest<int>;

    public sealed class EndlessHandler : IStreamRequestHandler<Endless, int>
    {
        public async IAsyncEnumerable<int> Handle(
            Endless request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (true)
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                yield return 1;
            }
        }
    }

    public sealed class WatchingHandler : IStreamRequestHandler<Watched, int>
    {
        public static CancellationToken Seen;

        public async IAsyncEnumerable<int> Handle(
            Watched request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Seen = cancellationToken;

            await Task.Yield();
            yield return 1;
        }
    }

    public sealed class ShieldedHandler : IStreamRequestHandler<Shielded, int>
    {
        public async IAsyncEnumerable<int> Handle(
            Shielded request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (int i = 0; i < 5; i++)
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                yield return i;
            }
        }
    }

    // Returns what Invoke hands back instead of iterating it, which is the pass-through shape.
    public sealed class ShieldingStage : IStreamRequestStage<Shielded, int>
    {
        public static CancellationToken Shield;

        public IAsyncEnumerable<int> Handle(
            Shielded request, StreamContinuation<int> next, CancellationToken cancellationToken)
            => next.Invoke(Shield);
    }

    // The only undecorated stream fixture in the suite, and the warning is suppressed rather than
    // fixed because the undecorated shape is the thing under test.
#pragma warning disable CS8425
    public sealed class UndecoratedHandler : IStreamRequestHandler<Undecorated, int>
    {
        public async IAsyncEnumerable<int> Handle(Undecorated request, CancellationToken cancellationToken)
        {
            while (true)
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                yield return 1;
            }
        }
    }
#pragma warning restore CS8425

    #endregion
}
