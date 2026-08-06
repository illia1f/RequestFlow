// .NET Framework has no GC.GetAllocatedBytesForCurrentThread, so the whole fixture is compiled out
// there and the other two targets cover it.
#if NET8_0_OR_GREATER
using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class ChainAllocationTests
{
    // A staged dispatch allocates nothing of RequestFlow's own, so the only bytes left are the
    // handler's task.
    [Fact]
    public async Task Given_A_Two_Stage_Chain_When_Dispatching_Then_It_Allocates_No_More_Than_A_Plain_Dispatch()
    {
        // Singletons: a transient stage is an allocation the container makes, not RequestFlow.
        IRequestDispatcher staged = Build(o =>
        {
            o.AddStage(typeof(OuterStage), s => s.AsSingleton());
            o.AddStage(typeof(InnerStage), s => s.AsSingleton());
        });
        IRequestDispatcher plain = Build();

        long stagedBytes = await MeasureAsync(() => staged.SendAsync<string>(new Ping()));
        long plainBytes = await MeasureAsync(() => plain.SendAsync<string>(new Bare()));

        stagedBytes.ShouldBe(plainBytes);
    }

    // One stage on its own, so a cost that grows per level is caught at the first level too.
    [Fact]
    public async Task Given_A_One_Stage_Chain_When_Dispatching_Then_It_Allocates_No_More_Than_A_Plain_Dispatch()
    {
        IRequestDispatcher staged = Build(o => o.AddStage(typeof(OuterStage), s => s.AsSingleton()));
        IRequestDispatcher plain = Build();

        long stagedBytes = await MeasureAsync(() => staged.SendAsync<string>(new Ping()));
        long plainBytes = await MeasureAsync(() => plain.SendAsync<string>(new Bare()));

        stagedBytes.ShouldBe(plainBytes);
    }

    // A void level hands the container's plain Task to the NoResult bridge. A completed task crosses
    // it on the cached task, so a chain that finishes synchronously pays nothing per level.
    [Fact]
    public async Task Given_A_Two_Stage_Void_Chain_That_Finishes_Synchronously_When_Dispatching_Then_It_Allocates_No_More_Than_A_Plain_Dispatch()
    {
        IRequestDispatcher staged = Build(o =>
        {
            o.AddStage(typeof(OuterSignalStage), s => s.AsSingleton());
            o.AddStage(typeof(InnerSignalStage), s => s.AsSingleton());
        });
        IRequestDispatcher plain = Build();

        long stagedBytes = await MeasureAsync(() => staged.SendAsync(new Signal()));
        long plainBytes = await MeasureAsync(() => plain.SendAsync(new BareSignal()));

        stagedBytes.ShouldBe(plainBytes);
    }

    // The typed shape has no bridge, so suspending changes nothing: the stage's task is the level's
    // task and passes through.
    [Fact]
    public async Task Given_A_Two_Stage_Typed_Chain_That_Suspends_When_Dispatching_Then_It_Allocates_No_More_Than_A_Plain_Dispatch()
    {
        IRequestDispatcher staged = Build(o =>
        {
            o.AddStage(typeof(OuterHeldStage), s => s.AsSingleton());
            o.AddStage(typeof(InnerHeldStage), s => s.AsSingleton());
        });
        IRequestDispatcher plain = Build();

        long stagedBytes = await MeasureAsync(() => GatedAsync(gate => staged.SendAsync<string>(new Held(gate))));
        long plainBytes = await MeasureAsync(() => GatedAsync(gate => plain.SendAsync<string>(new BareHeld(gate))));

        stagedBytes.ShouldBe(plainBytes);
    }

    // A pass-through void stage hands back the level's own Task<NoResult>, which the bridge takes as
    // it is, so a chain still running when it returns costs no more than one that finished.
    [Fact]
    public async Task Given_A_Two_Stage_Void_Chain_That_Suspends_When_Dispatching_Then_It_Allocates_No_More_Than_A_Plain_Dispatch()
    {
        IRequestDispatcher staged = Build(o =>
        {
            o.AddStage(typeof(OuterGatedStage), s => s.AsSingleton());
            o.AddStage(typeof(InnerGatedStage), s => s.AsSingleton());
        });
        IRequestDispatcher plain = Build();

        long stagedBytes = await MeasureAsync(() => GatedAsync(gate => staged.SendAsync(new Gated(gate))));
        long plainBytes = await MeasureAsync(() => GatedAsync(gate => plain.SendAsync(new BareGated(gate))));

        stagedBytes.ShouldBe(plainBytes);
    }

    // The cost that survives the cast. An async stage returns its own builder's plain Task, so a
    // suspended void level of that shape crosses the bridge on a Task<NoResult> of its own. Pinned
    // against the typed chain, where the same stage costs its state machine and nothing more, rather
    // than against a byte figure that differs per runtime.
    [Fact]
    public async Task Given_A_Void_Chain_Of_Async_Stages_That_Suspends_When_Dispatching_Then_Every_Level_Costs_More_Than_A_Typed_One()
    {
        IRequestDispatcher voidOne = Build(o => o.AddStage(typeof(OuterAwaitingStage), s => s.AsSingleton()));
        IRequestDispatcher voidTwo = Build(o =>
        {
            o.AddStage(typeof(OuterAwaitingStage), s => s.AsSingleton());
            o.AddStage(typeof(InnerAwaitingStage), s => s.AsSingleton());
        });
        IRequestDispatcher typedOne = Build(o => o.AddStage(typeof(OuterAwaitingHeldStage), s => s.AsSingleton()));
        IRequestDispatcher typedTwo = Build(o =>
        {
            o.AddStage(typeof(OuterAwaitingHeldStage), s => s.AsSingleton());
            o.AddStage(typeof(InnerAwaitingHeldStage), s => s.AsSingleton());
        });

        long voidLevel =
            await MeasureAsync(() => GatedAsync(gate => voidTwo.SendAsync(new Gated(gate))))
            - await MeasureAsync(() => GatedAsync(gate => voidOne.SendAsync(new Gated(gate))));
        long typedLevel =
            await MeasureAsync(() => GatedAsync(gate => typedTwo.SendAsync<string>(new Held(gate))))
            - await MeasureAsync(() => GatedAsync(gate => typedOne.SendAsync<string>(new Held(gate))));

        typedLevel.ShouldBeGreaterThan(0);
        voidLevel.ShouldBeGreaterThan(typedLevel);
    }

    #region Helpers

    private static IRequestDispatcher Build(Action<RequestFlowOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<ChainAllocationTests>();
            configure?.Invoke(o);
        });

        return services.BuildServiceProvider().CreateScope().ServiceProvider
            .GetRequiredService<IRequestDispatcher>();
    }

    // GC.GetAllocatedBytesForCurrentThread is exact for this thread, so one warmed dispatch is
    // measurable without BenchmarkDotNet. It also counts only this thread, so every fixture below has
    // to finish on the calling one: a handler that really suspends would take the second reading on a
    // pool thread and the delta would mean nothing.
    private static async Task<long> MeasureAsync(Func<Task> dispatch)
    {
        // Microsoft's container compiles its call sites in the background a few resolutions in, so
        // the loop keeps that changeover out of the measurement.
        for (int i = 0; i < 64; i++)
        {
            await dispatch();
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        await dispatch();

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    // The gate is released after every level has returned its task, so each one hands the bridge a
    // task that is still running. Completing it on this thread keeps the resulting allocations here,
    // where GC.GetAllocatedBytesForCurrentThread can see them.
    private static async Task GatedAsync(Func<TaskCompletionSource<string>, Task> dispatch)
    {
        var gate = new TaskCompletionSource<string>();

        Task walk = dispatch(gate);
        gate.SetResult("released");

        await walk;
    }

    public sealed record Ping : IRequest<string>;

    public sealed record Bare : IRequest<string>;

    public sealed record Signal : IRequest;

    public sealed record BareSignal : IRequest;

    public sealed record Gated(TaskCompletionSource<string> Gate) : IRequest;

    public sealed record BareGated(TaskCompletionSource<string> Gate) : IRequest;

    public sealed record Held(TaskCompletionSource<string> Gate) : IRequest<string>;

    public sealed record BareHeld(TaskCompletionSource<string> Gate) : IRequest<string>;

    public sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, CancellationToken cancellationToken)
            => Task.FromResult("ping");
    }

    public sealed class BareHandler : IRequestHandler<Bare, string>
    {
        public Task<string> HandleAsync(Bare request, CancellationToken cancellationToken)
            => Task.FromResult("bare");
    }

    public sealed class OuterStage : IRequestStage<Ping, string>
    {
        public Task<string> HandleAsync(
            Ping request, Continuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    public sealed class InnerStage : IRequestStage<Ping, string>
    {
        public Task<string> HandleAsync(
            Ping request, Continuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    public sealed class SignalHandler : IRequestHandler<Signal>
    {
        public Task HandleAsync(Signal request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class BareSignalHandler : IRequestHandler<BareSignal>
    {
        public Task HandleAsync(BareSignal request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class OuterSignalStage : IRequestStage<Signal>
    {
        public Task HandleAsync(Signal request, Continuation next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    public sealed class InnerSignalStage : IRequestStage<Signal>
    {
        public Task HandleAsync(Signal request, Continuation next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    public sealed class GatedHandler : IRequestHandler<Gated>
    {
        public Task HandleAsync(Gated request, CancellationToken cancellationToken)
            => request.Gate.Task;
    }

    public sealed class BareGatedHandler : IRequestHandler<BareGated>
    {
        public Task HandleAsync(BareGated request, CancellationToken cancellationToken)
            => request.Gate.Task;
    }

    public sealed class OuterGatedStage : IRequestStage<Gated>
    {
        public Task HandleAsync(Gated request, Continuation next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    public sealed class InnerGatedStage : IRequestStage<Gated>
    {
        public Task HandleAsync(Gated request, Continuation next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    public sealed class OuterAwaitingStage : IRequestStage<Gated>
    {
        public async Task HandleAsync(Gated request, Continuation next, CancellationToken cancellationToken)
            => await next.InvokeAsync(cancellationToken);
    }

    public sealed class InnerAwaitingStage : IRequestStage<Gated>
    {
        public async Task HandleAsync(Gated request, Continuation next, CancellationToken cancellationToken)
            => await next.InvokeAsync(cancellationToken);
    }

    public sealed class HeldHandler : IRequestHandler<Held, string>
    {
        public Task<string> HandleAsync(Held request, CancellationToken cancellationToken)
            => request.Gate.Task;
    }

    public sealed class BareHeldHandler : IRequestHandler<BareHeld, string>
    {
        public Task<string> HandleAsync(BareHeld request, CancellationToken cancellationToken)
            => request.Gate.Task;
    }

    public sealed class OuterHeldStage : IRequestStage<Held, string>
    {
        public Task<string> HandleAsync(
            Held request, Continuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    public sealed class InnerHeldStage : IRequestStage<Held, string>
    {
        public Task<string> HandleAsync(
            Held request, Continuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    public sealed class OuterAwaitingHeldStage : IRequestStage<Held, string>
    {
        public async Task<string> HandleAsync(
            Held request, Continuation<string> next, CancellationToken cancellationToken)
            => await next.InvokeAsync(cancellationToken);
    }

    public sealed class InnerAwaitingHeldStage : IRequestStage<Held, string>
    {
        public async Task<string> HandleAsync(
            Held request, Continuation<string> next, CancellationToken cancellationToken)
            => await next.InvokeAsync(cancellationToken);
    }

    #endregion
}
#endif
