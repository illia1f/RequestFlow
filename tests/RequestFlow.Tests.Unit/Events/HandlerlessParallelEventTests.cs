using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow.Tests.Unit.Events;

public sealed class HandlerlessParallelEventTests
{
#if NET8_0_OR_GREATER
#if DEBUG
    [Fact(Skip = "Debug allocates the async state machine. Run this allocation test in Release.")]
#else
    [Fact]
#endif
    public async Task Given_A_Parallel_Event_With_No_Handler_When_Publishing_Then_No_Dispatch_Allocation_Is_Added()
    {
        using var context = new Context();
        for (int i = 0; i < 64; i++) await context.Publish();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 64; i++) await context.Publish();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(0);
    }
#endif

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Parallel_Event_With_No_Handler_When_Publishing_Then_Publication_Succeeds(bool typedSelection)
    {
        using var context = new Context(typedSelection);

        Task result = context.Publish();
        await result;

        result.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Pre_Canceled_Publish_When_The_Parallel_Event_Has_No_Handler_Then_Publication_Is_Canceled(bool typedSelection)
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
        exception.SkippedHandlerCount.ShouldBe(0);
    }

    #region Helpers

    private sealed class Context : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;
        private readonly IEventPublisher _publisher;
        private readonly Notice _notice = new();

        public Context(bool typedSelection = false)
        {
            var services = new ServiceCollection();
            services.AddRequestFlow(options =>
            {
                options.AddEvent<Notice>().AllowUnhandledEvent<Notice>();
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

    public sealed class Notice : IEvent;

    #endregion
}
