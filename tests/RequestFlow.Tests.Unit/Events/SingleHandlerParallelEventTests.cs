using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow.Tests.Unit.Events;

public sealed class SingleHandlerParallelEventTests
{
#if NET8_0_OR_GREATER
#if DEBUG
    [Fact(Skip = "Debug allocates the async state machine. Run this allocation test in Release.")]
#else
    [Fact]
#endif
    public async Task Given_One_Synchronous_Parallel_Handler_When_Publishing_Then_No_Dispatch_Allocation_Is_Added()
    {
        using var context = new Context();
        for (int i = 0; i < 64; i++) await context.Publish();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 64; i++) await context.Publish();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(0);
        context.State.Calls.ShouldBe(128);
    }
#endif

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_One_Parallel_Handler_When_It_Faults_Then_Publication_Preserves_The_Handler_Failure(bool delayed)
    {
        using var context = new Context();
        var completion = new TaskCompletionSource<object?>();
        var failure = new InvalidOperationException("failure");
        context.State.Operation = completion.Task;
        if (!delayed) completion.SetException(failure);

        Task result = context.Publish();
        if (delayed)
        {
            result.IsCompleted.ShouldBeFalse();
            completion.SetException(failure);
        }
        var exception = await Should.ThrowAsync<EventPublishException>(() => result);

        result.IsFaulted.ShouldBeTrue();
        EventHandlerFailure entry = exception.Failures.ShouldHaveSingleItem();
        entry.Exception.ShouldBeSameAs(failure);
        entry.HandlerType.ShouldBe(typeof(Handler));
        entry.DeclaredEventType.ShouldBe(typeof(Notice));
        exception.Message.ShouldContain("1 of 1 handlers");
        exception.SkippedHandlerCount.ShouldBe(0);
        context.State.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Pre_Canceled_Publish_When_Selecting_One_Parallel_Handler_Then_The_Handler_Is_Not_Started(bool typedSelection)
    {
        using var context = new Context(typedSelection);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Task result = context.Publish(cancellation.Token);
        Exception? caught = null;
        try { await result; }
        catch (Exception observed) { caught = observed; }

        result.IsCanceled.ShouldBeTrue();
        var exception = caught.ShouldBeOfType<EventPublishCanceledException>();
        exception.CancellationToken.ShouldBe(cancellation.Token);
        exception.Failures.ShouldBeEmpty();
        context.State.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Given_Cancellation_After_The_Only_Parallel_Handler_Starts_When_It_Succeeds_Then_Publication_Succeeds()
    {
        using var context = new Context();
        using var cancellation = new CancellationTokenSource();
        var completion = new TaskCompletionSource<object?>();
        context.State.Operation = completion.Task;

        Task result = context.Publish(cancellation.Token);
        cancellation.Cancel();
        completion.SetResult(null);
        await result;

        result.Status.ShouldBe(TaskStatus.RanToCompletion);
        context.State.Token.ShouldBe(cancellation.Token);
        context.State.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Parallel_Handler_Cancellation_When_Publishing_Then_It_Remains_A_Handler_Failure(bool faultedCancellation)
    {
        using var context = new Context();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        context.State.Operation = faultedCancellation
            ? Task.FromException(new OperationCanceledException(cancellation.Token))
            : Task.FromCanceled(cancellation.Token);

        Task result = context.Publish();
        var exception = await Should.ThrowAsync<EventPublishException>(() => result);

        result.IsFaulted.ShouldBeTrue();
        exception.Failures.ShouldHaveSingleItem().Exception
            .ShouldBeAssignableTo<OperationCanceledException>().CancellationToken.ShouldBe(cancellation.Token);
    }

    #region Helpers

    private sealed class Context : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;
        private readonly IEventPublisher _publisher;
        private readonly Notice _notice = new();
        public State State { get; } = new();

        public Context(bool typedSelection = false)
        {
            var services = new ServiceCollection();
            services.AddSingleton(State);
            services.AddRequestFlow(options =>
            {
                options.WithScopedHandlers().AddEventHandler<Handler>();
                if (typedSelection) options.PublishEventsInParallel<Notice>();
                else options.PublishEventsInParallel();
            });
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
            _scope = _provider.CreateScope();
            _publisher = _scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        }

        public Task Publish(CancellationToken token = default) => _publisher.PublishAsync(_notice, token);
        public void Dispose()
        {
            _scope.Dispose();
            _provider.Dispose();
        }
    }

    public sealed class State
    {
        public Task Operation { get; set; } = Task.CompletedTask;
        public int Calls { get; set; }
        public CancellationToken Token { get; set; }
    }

    public sealed class Notice : IEvent;

    public sealed class Handler(State state) : IEventHandler<Notice>
    {
        public Task HandleAsync(Notice @event, CancellationToken cancellationToken)
        {
            state.Calls++;
            state.Token = cancellationToken;
            return state.Operation;
        }
    }

    #endregion
}
