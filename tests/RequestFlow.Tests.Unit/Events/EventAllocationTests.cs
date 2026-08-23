// .NET Framework has no GC.GetAllocatedBytesForCurrentThread, so the dedicated fixture
// is compiled only where the runtime can make the measurement.
#if NET8_0_OR_GREATER
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace RequestFlow.Tests.Unit.Events;

public sealed class EventAllocationTests
{
    [Fact]
    public void Given_Sequential_Handlers_Complete_Synchronously_When_Publishing_Then_Only_The_Public_Struct_Box_Is_Allocated()
    {
        using MeasurementContext context = Build(parallel: false);
        var classEvent = new ClassAllocationEvent(new AllocationState());
        var structEvent = new StructAllocationEvent(new AllocationState());
        Func<Task> publishClass = () => context.Publisher.PublishAsync(classEvent);
        Func<Task> publishStruct = () => context.Publisher.PublishAsync(structEvent);
        Func<Task> referenceClass = () => context.Reference.PublishSequentialAsync(
            classEvent, context.Services);
        Func<Task> referenceStruct = () => context.Reference.PublishSequentialAsync(
            structEvent, context.Services);

        publishClass().ShouldBeSameAs(Task.CompletedTask);
        publishStruct().ShouldBeSameAs(Task.CompletedTask);
        long classBytes = MeasureSynchronous(publishClass);
        long structBytes = MeasureSynchronous(publishStruct);
        long referenceClassBytes = MeasureSynchronous(referenceClass);
        long referenceStructBytes = MeasureSynchronous(referenceStruct);

        WriteMeasurements(
            "Sequential sync",
            classBytes,
            structBytes,
            referenceClassBytes,
            referenceStructBytes);
        classBytes.ShouldBe(referenceClassBytes);
        structBytes.ShouldBe(referenceStructBytes);
        (structBytes - classBytes).ShouldBe(referenceStructBytes - referenceClassBytes);
        structBytes.ShouldBeGreaterThan(classBytes);
    }

    [Fact]
    public void Given_Sequential_Handlers_Suspend_When_Publishing_Then_One_Publisher_Async_State_Is_Allocated_Per_Publish()
    {
        using MeasurementContext context = Build(parallel: false);
        var classEvent = new ClassAllocationEvent(new AllocationState());
        var structEvent = new StructAllocationEvent(new AllocationState());
        Func<TaskCompletionSource<object?>, Task> publishClassOnce = gate =>
            PublishWithOneSuspension(context.Publisher, classEvent, gate);
        Func<TaskCompletionSource<object?>, TaskCompletionSource<object?>, Task> publishClassTwice =
            (first, second) => PublishWithTwoSuspensions(
                context.Publisher, classEvent, first, second);
        Func<TaskCompletionSource<object?>, Task> publishStructOnce = gate =>
            PublishWithOneSuspension(context.Publisher, structEvent, gate);
        Func<TaskCompletionSource<object?>, TaskCompletionSource<object?>, Task> publishStructTwice =
            (first, second) => PublishWithTwoSuspensions(
                context.Publisher, structEvent, first, second);
        Func<TaskCompletionSource<object?>, Task> referenceClass = gate =>
            PublishSequentialReferenceWithOneSuspension(
                context.Reference, context.Services, classEvent, gate);
        Func<TaskCompletionSource<object?>, Task> referenceStruct = gate =>
            PublishSequentialReferenceWithOneSuspension(
                context.Reference, context.Services, structEvent, gate);

        long classBytes = MeasureOneSuspension(publishClassOnce);
        long classTwiceBytes = MeasureTwoSuspensions(publishClassTwice);
        long structBytes = MeasureOneSuspension(publishStructOnce);
        long structTwiceBytes = MeasureTwoSuspensions(publishStructTwice);
        long referenceClassBytes = MeasureOneSuspension(referenceClass);
        long referenceStructBytes = MeasureOneSuspension(referenceStruct);

        WriteMeasurements(
            "Sequential suspension",
            classBytes,
            structBytes,
            referenceClassBytes,
            referenceStructBytes,
            classTwiceBytes,
            structTwiceBytes);
        classBytes.ShouldBe(referenceClassBytes);
        structBytes.ShouldBe(referenceStructBytes);
        classTwiceBytes.ShouldBe(classBytes);
        structTwiceBytes.ShouldBe(structBytes);
        (structBytes - classBytes).ShouldBe(referenceStructBytes - referenceClassBytes);
        structBytes.ShouldBeGreaterThan(classBytes);
    }

    [Fact]
    public void Given_Parallel_Handlers_Complete_Synchronously_When_Publishing_Then_Only_The_Entry_Array_And_Public_Struct_Box_Are_Allocated()
    {
        using MeasurementContext context = Build(parallel: true);
        var classEvent = new ClassAllocationEvent(new AllocationState());
        var structEvent = new StructAllocationEvent(new AllocationState());
        Func<Task> publishClass = () => context.Publisher.PublishAsync(classEvent);
        Func<Task> publishStruct = () => context.Publisher.PublishAsync(structEvent);
        Func<Task> referenceClass = () => context.Reference.PublishParallelAsync(
            classEvent, context.Services);
        Func<Task> referenceStruct = () => context.Reference.PublishParallelAsync(
            structEvent, context.Services);

        long classBytes = MeasureSynchronous(publishClass);
        long structBytes = MeasureSynchronous(publishStruct);
        long referenceClassBytes = MeasureSynchronous(referenceClass);
        long referenceStructBytes = MeasureSynchronous(referenceStruct);

        WriteMeasurements(
            "Parallel sync",
            classBytes,
            structBytes,
            referenceClassBytes,
            referenceStructBytes);
        classBytes.ShouldBe(referenceClassBytes);
        structBytes.ShouldBe(referenceStructBytes);
        (structBytes - classBytes).ShouldBe(referenceStructBytes - referenceClassBytes);
        structBytes.ShouldBeGreaterThan(classBytes);
    }

    [Fact]
    public void Given_Parallel_Handlers_Suspend_When_Publishing_Then_One_Wrapper_Async_State_Is_Allocated_Per_Publish()
    {
        using MeasurementContext context = Build(parallel: true);
        var classEvent = new ClassAllocationEvent(new AllocationState());
        var structEvent = new StructAllocationEvent(new AllocationState());
        Func<TaskCompletionSource<object?>, Task> publishClassOnce = gate =>
            PublishWithOneSuspension(context.Publisher, classEvent, gate);
        Func<TaskCompletionSource<object?>, TaskCompletionSource<object?>, Task> publishClassTwice =
            (first, second) => PublishWithTwoSuspensions(
                context.Publisher, classEvent, first, second);
        Func<TaskCompletionSource<object?>, Task> publishStructOnce = gate =>
            PublishWithOneSuspension(context.Publisher, structEvent, gate);
        Func<TaskCompletionSource<object?>, TaskCompletionSource<object?>, Task> publishStructTwice =
            (first, second) => PublishWithTwoSuspensions(
                context.Publisher, structEvent, first, second);
        Func<TaskCompletionSource<object?>, Task> referenceClass = gate =>
            PublishParallelReferenceWithOneSuspension(
                context.Reference, context.Services, classEvent, gate);
        Func<TaskCompletionSource<object?>, Task> referenceStruct = gate =>
            PublishParallelReferenceWithOneSuspension(
                context.Reference, context.Services, structEvent, gate);

        long classBytes = MeasureOneSuspension(publishClassOnce);
        long classTwiceBytes = MeasureTwoSuspensions(publishClassTwice);
        long structBytes = MeasureOneSuspension(publishStructOnce);
        long structTwiceBytes = MeasureTwoSuspensions(publishStructTwice);
        long referenceClassBytes = MeasureOneSuspension(referenceClass);
        long referenceStructBytes = MeasureOneSuspension(referenceStruct);

        WriteMeasurements(
            "Parallel suspension",
            classBytes,
            structBytes,
            referenceClassBytes,
            referenceStructBytes,
            classTwiceBytes,
            structTwiceBytes);
        classBytes.ShouldBe(referenceClassBytes);
        structBytes.ShouldBe(referenceStructBytes);
        classTwiceBytes.ShouldBe(classBytes);
        structTwiceBytes.ShouldBe(structBytes);
        (structBytes - classBytes).ShouldBe(referenceStructBytes - referenceClassBytes);
        structBytes.ShouldBeGreaterThan(classBytes);
    }

    [Fact]
    public void Given_A_Custom_Strategy_And_Synchronous_Handlers_When_Publishing_Then_The_Strategy_Path_Budget_Is_Pinned()
    {
        using MeasurementContext specialized = Build(parallel: false);
        using MeasurementContext custom = Build(parallel: false, customStrategy: true);
        var specializedClassEvent = new ClassAllocationEvent(new AllocationState());
        var specializedStructEvent = new StructAllocationEvent(new AllocationState());
        var customClassEvent = new ClassAllocationEvent(new AllocationState());
        var customStructEvent = new StructAllocationEvent(new AllocationState());
        Func<Task> publishSpecializedClass = () => specialized.Publisher.PublishAsync(
            specializedClassEvent);
        Func<Task> publishSpecializedStruct = () => specialized.Publisher.PublishAsync(
            specializedStructEvent);
        Func<Task> publishCustomClass = () => custom.Publisher.PublishAsync(customClassEvent);
        Func<Task> publishCustomStruct = () => custom.Publisher.PublishAsync(customStructEvent);

        long specializedClassBytes = MeasureSynchronous(publishSpecializedClass);
        long specializedStructBytes = MeasureSynchronous(publishSpecializedStruct);
        long customClassBytes = MeasureSynchronous(publishCustomClass);
        long customStructBytes = MeasureSynchronous(publishCustomStruct);

        WriteMeasurements(
            "Custom strategy sync",
            customClassBytes,
            customStructBytes,
            specializedClassBytes,
            specializedStructBytes);
        // In Debug the compiler turns each async publish walk into a heap-allocated state machine
        // object, and the custom path's machine also carries the EventDelivery struct, which makes
        // it 24 bytes bigger than the specialized plan's. In Release both machines are structs that
        // stay on the stack when the publish completes synchronously, so neither path allocates and the difference is zero.
#if DEBUG
        const long strategyOverhead = 24;
#else
        const long strategyOverhead = 0;
#endif
        (customClassBytes - specializedClassBytes).ShouldBe(strategyOverhead);
        (customStructBytes - specializedStructBytes).ShouldBe(strategyOverhead);
        (customStructBytes - customClassBytes)
            .ShouldBe(specializedStructBytes - specializedClassBytes);
    }

    [Fact]
    public void Given_A_Synchronous_Entry_When_Starting_Through_A_Delivery_Then_The_Null_Result_Task_Allocates_Nothing()
    {
        var model = new EventModel(
            typeof(ClassAllocationEvent),
            [new EventHandlerModel(
                typeof(AlphaAllocationHandler),
                typeof(IAllocationEvent),
                RequestFlowLifetime.Singleton)]);
        var plan = new DeliveryAllocationPlan(
            model,
            [(_, _, _) => Task.CompletedTask]);
        EventDelivery delivery = plan.Create(
            new ClassAllocationEvent(new AllocationState()),
            Substitute.For<IServiceProvider>());
        Func<Task> start = () => delivery.StartAsync(0);

        long allocated = MeasureSynchronous(start);

        allocated.ShouldBe(0);
    }

    #region Initialization

    private readonly ITestOutputHelper _output;

    public EventAllocationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #endregion

    #region Helpers

    private const int WarmupIterations = 256;

    // A contended run can add stray bytes to a sample (a blocking wait when a continuation loses
    // the inline race, a tier transition), but never remove any, so the smallest of several
    // samples is the publish's own cost.
    private const int MeasurementSamples = 16;

    private static MeasurementContext Build(bool parallel, bool customStrategy = false)
    {
        var first = new AlphaAllocationHandler();
        var second = new ZuluAllocationHandler();
        var strategy = new AllocationPublishStrategy();
        var services = new ServiceCollection();
        services.AddSingleton(first);
        services.AddSingleton(second);
        services.AddSingleton(strategy);
        ServiceProvider provider = services.BuildServiceProvider();
        var registry = new RequestFlowRegistry();
        registry.Add(
            [],
            [],
            [
                RegistrationFor<AlphaAllocationHandler>(),
                RegistrationFor<ZuluAllocationHandler>(),
            ],
            [typeof(ClassAllocationEvent), typeof(StructAllocationEvent)],
            []);
        if (customStrategy)
        {
            registry.AddEventStrategyDeclarations([
                new EventStrategyDeclaration(
                    declaredEventType: null,
                    typeof(AllocationPublishStrategy),
                    ServiceLifetime.Singleton),
            ]);
        }
        else if (parallel)
        {
            registry.AddEventStrategyDeclarations([
                new EventStrategyDeclaration(
                    declaredEventType: null,
                    typeof(ParallelPublishStrategy),
                    ServiceLifetime.Singleton),
            ]);
        }

        FrozenPlans plans = registry.Freeze(provider);
        var publisher = new EventPublisher(plans.Events, provider);

        return new MeasurementContext(
            provider, publisher, new ReferenceEventPlan(typeof(IAllocationEvent)));
    }

    private static EventHandlerRegistration RegistrationFor<THandler>()
        => new(
            new EventHandlerDiscovery(typeof(THandler), typeof(IAllocationEvent)),
            ServiceLifetime.Singleton);

    private static long MeasureSynchronous(Func<Task> operation)
    {
        for (int i = 0; i < WarmupIterations; i++)
        {
            Task warmup = operation();
            if (!warmup.IsCompletedSuccessfully)
                throw new InvalidOperationException("The synchronous sample suspended during warm-up.");

            warmup.GetAwaiter().GetResult();
        }

        long best = long.MaxValue;
        for (int i = 0; i < MeasurementSamples; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            Task task = operation();
            bool completedSynchronously = task.IsCompletedSuccessfully;
            task.GetAwaiter().GetResult();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            completedSynchronously.ShouldBeTrue();
            best = Math.Min(best, allocated);
        }

        return best;
    }

    // The gate and delegates are built before the first reading. SetResult runs the publisher's
    // continuation inline, so both allocation readings stay on this thread.
    private static long MeasureOneSuspension(
        Func<TaskCompletionSource<object?>, Task> start)
    {
        for (int i = 0; i < WarmupIterations; i++)
        {
            var warmupGate = new TaskCompletionSource<object?>();
            Task warmup = start(warmupGate);
            if (warmup.IsCompleted)
                throw new InvalidOperationException("The suspension sample completed during warm-up.");

            warmupGate.SetResult(null);
            warmup.GetAwaiter().GetResult();
        }

        long best = long.MaxValue;
        for (int i = 0; i < MeasurementSamples; i++)
        {
            var gate = new TaskCompletionSource<object?>();
            long before = GC.GetAllocatedBytesForCurrentThread();
            Task task = start(gate);
            bool suspended = !task.IsCompleted;
            gate.SetResult(null);
            task.GetAwaiter().GetResult();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            suspended.ShouldBeTrue();
            best = Math.Min(best, allocated);
        }

        return best;
    }

    // The second gate is still incomplete when the first continuation reaches it. The same async
    // state object must carry both waits.
    private static long MeasureTwoSuspensions(
        Func<TaskCompletionSource<object?>, TaskCompletionSource<object?>, Task> start)
    {
        for (int i = 0; i < WarmupIterations; i++)
        {
            var firstWarmupGate = new TaskCompletionSource<object?>();
            var secondWarmupGate = new TaskCompletionSource<object?>();
            Task warmup = start(firstWarmupGate, secondWarmupGate);
            if (warmup.IsCompleted)
                throw new InvalidOperationException("The first suspension did not hold during warm-up.");

            firstWarmupGate.SetResult(null);
            if (warmup.IsCompleted)
                throw new InvalidOperationException("The second suspension did not hold during warm-up.");

            secondWarmupGate.SetResult(null);
            warmup.GetAwaiter().GetResult();
        }

        long best = long.MaxValue;
        for (int i = 0; i < MeasurementSamples; i++)
        {
            var firstGate = new TaskCompletionSource<object?>();
            var secondGate = new TaskCompletionSource<object?>();
            long before = GC.GetAllocatedBytesForCurrentThread();
            Task task = start(firstGate, secondGate);
            bool firstSuspended = !task.IsCompleted;
            firstGate.SetResult(null);
            bool secondSuspended = !task.IsCompleted;
            secondGate.SetResult(null);
            task.GetAwaiter().GetResult();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            firstSuspended.ShouldBeTrue();
            secondSuspended.ShouldBeTrue();
            best = Math.Min(best, allocated);
        }

        return best;
    }

    private static Task PublishWithOneSuspension<TEvent>(
        IEventPublisher publisher,
        TEvent @event,
        TaskCompletionSource<object?> gate)
        where TEvent : IAllocationEvent
    {
        @event.State.First = gate.Task;
        @event.State.Second = Task.CompletedTask;
        return publisher.PublishAsync(@event);
    }

    private static Task PublishWithTwoSuspensions<TEvent>(
        IEventPublisher publisher,
        TEvent @event,
        TaskCompletionSource<object?> first,
        TaskCompletionSource<object?> second)
        where TEvent : IAllocationEvent
    {
        @event.State.First = first.Task;
        @event.State.Second = second.Task;
        return publisher.PublishAsync(@event);
    }

    private static Task PublishSequentialReferenceWithOneSuspension<TEvent>(
        ReferenceEventPlan reference,
        IServiceProvider services,
        TEvent @event,
        TaskCompletionSource<object?> gate)
        where TEvent : IAllocationEvent
    {
        @event.State.First = gate.Task;
        @event.State.Second = Task.CompletedTask;
        return reference.PublishSequentialAsync(@event, services);
    }

    private static Task PublishParallelReferenceWithOneSuspension<TEvent>(
        ReferenceEventPlan reference,
        IServiceProvider services,
        TEvent @event,
        TaskCompletionSource<object?> gate)
        where TEvent : IAllocationEvent
    {
        @event.State.First = gate.Task;
        @event.State.Second = Task.CompletedTask;
        return reference.PublishParallelAsync(@event, services);
    }

    private void WriteMeasurements(
        string scenario,
        long classBytes,
        long structBytes,
        long referenceClassBytes,
        long referenceStructBytes,
        long? secondClassBytes = null,
        long? secondStructBytes = null)
    {
        _output.WriteLine(
            "{0}: class={1}, struct={2}, box delta={3}, reference class={4}, reference struct={5}",
            scenario,
            classBytes,
            structBytes,
            structBytes - classBytes,
            referenceClassBytes,
            referenceStructBytes);

        if (secondClassBytes.HasValue && secondStructBytes.HasValue)
        {
            _output.WriteLine(
                "{0}: two suspensions class={1}, struct={2}",
                scenario,
                secondClassBytes.Value,
                secondStructBytes.Value);
        }
    }

    private interface IAllocationEvent : IEvent
    {
        AllocationState State { get; }
    }

    private sealed record ClassAllocationEvent(AllocationState State) : IAllocationEvent;

    private readonly record struct StructAllocationEvent(AllocationState State) : IAllocationEvent;

    private sealed class AllocationState
    {
        public Task First { get; set; } = Task.CompletedTask;

        public Task Second { get; set; } = Task.CompletedTask;
    }

    private sealed class AlphaAllocationHandler : IEventHandler<IAllocationEvent>
    {
        public Task HandleAsync(
            IAllocationEvent @event,
            CancellationToken cancellationToken)
            => @event.State.First;
    }

    private sealed class ZuluAllocationHandler : IEventHandler<IAllocationEvent>
    {
        public Task HandleAsync(
            IAllocationEvent @event,
            CancellationToken cancellationToken)
            => @event.State.Second;
    }

    private sealed class AllocationPublishStrategy : IEventPublishStrategy
    {
        private readonly SequentialPublishStrategy _inner = new();

        public Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken)
            => _inner.PublishAsync(delivery, cancellationToken);
    }

    private sealed class DeliveryAllocationPlan(EventModel model, HandlerEntry[] entries)
        : EventPlan(model, entries)
    {
        public EventDelivery Create(IEvent @event, IServiceProvider services)
            => Delivery(@event, services, default);

        public override Task ExecuteAsync(
            IEvent @event,
            IServiceProvider services,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class MeasurementContext(
        ServiceProvider provider,
        IEventPublisher publisher,
        ReferenceEventPlan reference) : IDisposable
    {
        public IServiceProvider Services { get; } = provider;

        public IEventPublisher Publisher { get; } = publisher;

        public ReferenceEventPlan Reference { get; } = reference;

        public void Dispose()
        {
            provider.Dispose();
        }
    }

    // Mirrors the plan under measurement: the event type is known before the publish, so it
    // costs no field in the async state machine.
    private sealed class ReferenceEventPlan(Type eventType)
    {
        public async Task PublishSequentialAsync(
            IEvent @event,
            IServiceProvider services,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            List<Exception>? failures = null;
            for (int i = 0; i < 2; i++)
            {
                if (i > 0 && cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);

                Task task = Start(i, @event, services, cancellationToken);
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failures ??= [];
                    failures.Add(task.IsFaulted ? task.Exception! : exception);
                }
            }

            if (failures is not null)
                throw new AggregateException(eventType.FullName, failures);
        }

        public async Task PublishParallelAsync(
            IEvent @event,
            IServiceProvider services,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            var tasks = new Task[2];
            for (int i = 0; i < tasks.Length; i++)
                tasks[i] = Start(i, @event, services, cancellationToken);

            List<Exception>? failures = null;
            for (int i = 0; i < tasks.Length; i++)
            {
                Task task = tasks[i];
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failures ??= [];
                    failures.Add(task.IsFaulted ? task.Exception! : exception);
                }
            }

            if (failures is not null)
                throw new AggregateException(eventType.FullName, failures);
        }

        private Task Start(
            int index,
            IEvent @event,
            IServiceProvider services,
            CancellationToken cancellationToken)
        {
            var allocationEvent = (IAllocationEvent)@event;
            return index switch
            {
                0 => ((AlphaAllocationHandler)services.GetRequiredService(
                    typeof(AlphaAllocationHandler))).HandleAsync(
                        allocationEvent, cancellationToken),
                _ => ((ZuluAllocationHandler)services.GetRequiredService(
                    typeof(ZuluAllocationHandler))).HandleAsync(
                        allocationEvent, cancellationToken),
            };
        }
    }

    #endregion
}
#endif
