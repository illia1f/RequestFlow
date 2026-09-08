// .NET Framework has no GC.GetAllocatedBytesForCurrentThread, so the modern targets own this fixture.
#if NET8_0_OR_GREATER
using Microsoft.Extensions.DependencyInjection;
using RequestFlow;
using Xunit.Abstractions;

namespace RequestFlow.Tests.Unit;

public sealed class ValueChainAllocationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Given_A_Synchronous_Typed_Value_Chain_When_Dispatching_Then_It_Allocates_Zero_Bytes(int stageCount)
    {
        Type[] stageTypes = stageCount switch
        {
            0 => [],
            1 => [typeof(FirstTypedStage)],
            _ => [typeof(FirstTypedStage), typeof(SecondTypedStage)],
        };
        using ServiceProvider provider = BuildTypedDispatcher(
            out IValueRequestDispatcher dispatcher, stageTypes);

        long bytes = Measure(() => dispatcher.SendAsync(TypedRequestInstance));

        output.WriteLine($"typed-handler-{stageCount}-stages: {bytes}");
        bytes.ShouldBe(0);
    }

    [Fact]
    public void Given_Synchronous_Plain_Void_Value_Handler_Without_Stages_When_Dispatching_Then_It_Allocates_Zero_Bytes()
    {
        using ServiceProvider provider = BuildVoidDispatcher(
            out IValueRequestDispatcher dispatcher,
            typedShapes: []);

        long bytes = Measure(() => dispatcher.SendAsync(VoidRequestInstance));

        output.WriteLine($"plain-void-handler-no-stages: {bytes}");
        bytes.ShouldBe(0);
    }

    [Fact]
    public void Given_Synchronous_Plain_Void_Value_Handler_With_Two_Plain_Stages_When_Dispatching_Then_It_Allocates_Zero_Bytes()
    {
        using ServiceProvider provider = BuildVoidDispatcher(
            out IValueRequestDispatcher dispatcher,
            typedShapes: [false, false],
            typeof(FirstPlainVoidStage),
            typeof(SecondPlainVoidStage));

        long bytes = Measure(() => dispatcher.SendAsync(VoidRequestInstance));

        output.WriteLine($"plain-void-handler-two-plain-stages: {bytes}");
        bytes.ShouldBe(0);
    }

    [Fact]
    public void Given_Synchronous_Plain_Void_Value_Handler_With_Typed_And_Plain_Stages_When_Dispatching_Then_It_Allocates_Zero_Bytes()
    {
        using ServiceProvider provider = BuildVoidDispatcher(
            out IValueRequestDispatcher dispatcher,
            typedShapes: [true, false],
            typeof(TypedNoResultStage),
            typeof(FirstPlainVoidStage));

        long bytes = Measure(() => dispatcher.SendAsync(VoidRequestInstance));

        output.WriteLine($"plain-void-handler-typed-and-plain-stages: {bytes}");
        bytes.ShouldBe(0);
    }

    [Fact]
    public void Given_A_Genuinely_Awaiting_Value_Stage_When_Dispatching_A_Suspended_Handler_Then_It_Allocates_More_Than_The_Handler_Alone()
    {
        using ServiceProvider plainProvider = BuildSuspendedDispatcher(
            staged: false,
            out IValueRequestDispatcher plain,
            out SuspendedHandler plainHandler);
        using ServiceProvider stagedProvider = BuildSuspendedDispatcher(
            staged: true,
            out IValueRequestDispatcher staged,
            out SuspendedHandler stagedHandler);

        long plainBytes = MeasureSuspended(plain, plainHandler);
        long stagedBytes = MeasureSuspended(staged, stagedHandler);

        output.WriteLine($"suspended-handler-no-stage: {plainBytes}");
        output.WriteLine($"suspended-handler-awaiting-stage: {stagedBytes}");
        stagedBytes.ShouldBeGreaterThan(plainBytes);
    }

    #region Initialization

    private static readonly TypedRequest TypedRequestInstance = new();
    private static readonly VoidRequest VoidRequestInstance = new();
    private static readonly SuspendedRequest SuspendedRequestInstance = new();

    #endregion

    #region Helpers

    private static ServiceProvider BuildTypedDispatcher(
        out IValueRequestDispatcher dispatcher,
        params Type[] stageTypes)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IValueRequestHandler<TypedRequest, string>>(new TypedHandler());
        foreach (Type stageType in stageTypes)
        {
            services.AddSingleton(stageType);
        }

        ServiceProvider provider = services.BuildServiceProvider();
        RequestPlanBase plan = stageTypes.Length == 0
            ? new ValueRequestPlan<TypedRequest, string>()
            : new StagedValueRequestPlan<TypedRequest, string>(new StageChain(stageTypes, []));
        dispatcher = new ValueRequestDispatcher(
            new DispatchMap(new Dictionary<Type, RequestPlanBase>
            {
                [typeof(TypedRequest)] = plan,
            }),
            provider);

        return provider;
    }

    private static ServiceProvider BuildVoidDispatcher(
        out IValueRequestDispatcher dispatcher,
        bool[] typedShapes,
        params Type[] stageTypes)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IValueRequestHandler<VoidRequest>>(new VoidHandler());
        foreach (Type stageType in stageTypes)
        {
            services.AddSingleton(stageType);
        }

        ServiceProvider provider = services.BuildServiceProvider();
        RequestPlanBase plan = stageTypes.Length == 0
            ? new ValueVoidRequestPlan<VoidRequest>()
            : new StagedValueVoidRequestPlan<VoidRequest>(
                new StageChain(stageTypes, typedShapes));
        dispatcher = new ValueRequestDispatcher(
            new DispatchMap(new Dictionary<Type, RequestPlanBase>
            {
                [typeof(VoidRequest)] = plan,
            }),
            provider);

        return provider;
    }

    private static ServiceProvider BuildSuspendedDispatcher(
        bool staged,
        out IValueRequestDispatcher dispatcher,
        out SuspendedHandler handler)
    {
        handler = new SuspendedHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IValueRequestHandler<SuspendedRequest, string>>(handler);
        if (staged)
            services.AddSingleton<AwaitingStage>();

        ServiceProvider provider = services.BuildServiceProvider();
        RequestPlanBase plan = staged
            ? new StagedValueRequestPlan<SuspendedRequest, string>(
                new StageChain([typeof(AwaitingStage)], []))
            : new ValueRequestPlan<SuspendedRequest, string>();
        dispatcher = new ValueRequestDispatcher(
            new DispatchMap(new Dictionary<Type, RequestPlanBase>
            {
                [typeof(SuspendedRequest)] = plan,
            }),
            provider);

        return provider;
    }

    private static long Measure<T>(Func<ValueTask<T>> dispatch)
    {
        for (int i = 0; i < 64; i++)
            dispatch().GetAwaiter().GetResult();

        long before = GC.GetAllocatedBytesForCurrentThread();
        dispatch().GetAwaiter().GetResult();

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static long Measure(Func<ValueTask> dispatch)
    {
        for (int i = 0; i < 64; i++)
            dispatch().GetAwaiter().GetResult();

        long before = GC.GetAllocatedBytesForCurrentThread();
        dispatch().GetAwaiter().GetResult();

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static long MeasureSuspended(
        IValueRequestDispatcher dispatcher,
        SuspendedHandler handler)
    {
        for (int i = 0; i < 64; i++)
        {
            handler.Prepare();
            ValueTask<string> pending = dispatcher.SendAsync(SuspendedRequestInstance);
            handler.Complete();
            pending.GetAwaiter().GetResult();
        }

        handler.Prepare();
        long before = GC.GetAllocatedBytesForCurrentThread();
        ValueTask<string> measured = dispatcher.SendAsync(SuspendedRequestInstance);
        handler.Complete();
        measured.GetAwaiter().GetResult();

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private sealed record TypedRequest : IValueRequest<string>;

    private sealed record VoidRequest : IValueRequest;

    private sealed record SuspendedRequest : IValueRequest<string>;

    private sealed class TypedHandler : IValueRequestHandler<TypedRequest, string>
    {
        public ValueTask<string> HandleAsync(
            TypedRequest request,
            CancellationToken cancellationToken)
            => new("typed");
    }

    private sealed class VoidHandler : IValueRequestHandler<VoidRequest>
    {
        public ValueTask HandleAsync(
            VoidRequest request,
            CancellationToken cancellationToken)
            => default;
    }

    private sealed class SuspendedHandler : IValueRequestHandler<SuspendedRequest, string>
    {
        private TaskCompletionSource<string> _gate = null!;

        public void Prepare()
            => _gate = new TaskCompletionSource<string>();

        public void Complete()
            => _gate.SetResult("released");

        public ValueTask<string> HandleAsync(
            SuspendedRequest request,
            CancellationToken cancellationToken)
            => new(_gate.Task);
    }

    private sealed class FirstTypedStage : IValueRequestStage<TypedRequest, string>
    {
        public ValueTask<string> HandleAsync(
            TypedRequest request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed class SecondTypedStage : IValueRequestStage<TypedRequest, string>
    {
        public ValueTask<string> HandleAsync(
            TypedRequest request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed class FirstPlainVoidStage : IValueRequestStage<VoidRequest>
    {
        public ValueTask HandleAsync(
            VoidRequest request,
            ValueContinuation next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed class SecondPlainVoidStage : IValueRequestStage<VoidRequest>
    {
        public ValueTask HandleAsync(
            VoidRequest request,
            ValueContinuation next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed class TypedNoResultStage : IValueRequestStage<VoidRequest, NoResult>
    {
        public ValueTask<NoResult> HandleAsync(
            VoidRequest request,
            ValueContinuation<NoResult> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed class AwaitingStage : IValueRequestStage<SuspendedRequest, string>
    {
        public async ValueTask<string> HandleAsync(
            SuspendedRequest request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken)
            => await next.InvokeAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion
}
#endif
