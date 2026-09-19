using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow.Tests.Unit;

public sealed class StreamFailureCompatibilityTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Given_Move_Next_Faulted_With_Cancellation_When_Enumerating_Then_The_Outer_Move_Is_Canceled_And_Disposes_The_Inner(
        bool dispatchToken, bool failsLater)
    {
        using ServiceProvider provider = Build();
        using IServiceScope scope = provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IStreamDispatcher>();
        using var source = new CancellationTokenSource();
        source.Cancel();
        var failure = new OperationCanceledException(source.Token);
        var request = new Pending();
        if (!failsLater)
            request.Completion.SetException(failure);
        await using IAsyncEnumerator<int> enumerator = dispatcher
            .Stream(request, dispatchToken ? source.Token : CancellationToken.None).GetAsyncEnumerator();

        Task<bool> move = enumerator.MoveNextAsync().AsTask();
        if (failsLater)
        {
            move.IsCompleted.ShouldBeFalse();
            request.Completion.SetException(failure);
        }

        await Should.ThrowAsync<OperationCanceledException>(() => move);
        move.IsCanceled.ShouldBeTrue();
        request.Disposals.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_Move_Next_Faulted_With_Cancellation_When_Awaiting_The_Value_Task_Then_The_Original_Exception_And_Token_Are_Preserved(bool dispatchToken)
    {
        using ServiceProvider provider = Build();
        using IServiceScope scope = provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IStreamDispatcher>();
        using var source = new CancellationTokenSource();
        source.Cancel();
        var failure = new OperationCanceledException(source.Token);
        var request = new Pending();
        await using IAsyncEnumerator<int> enumerator = dispatcher
            .Stream(request, dispatchToken ? source.Token : CancellationToken.None).GetAsyncEnumerator();

        ValueTask<bool> move = enumerator.MoveNextAsync();
        request.Completion.SetException(failure);

        OperationCanceledException? exception = null;
        try { await move; }
        catch (OperationCanceledException caught) { exception = caught; }
        exception.ShouldBeSameAs(failure);
        exception!.CancellationToken.ShouldBe(source.Token);
        request.Disposals.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Stream_Disposed_After_One_Item_When_Stopping_Early_Then_The_Inner_Enumerator_Is_Disposed(bool dispatchToken)
    {
        using ServiceProvider provider = Build();
        using IServiceScope scope = provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IStreamDispatcher>();
        using var source = new CancellationTokenSource();
        var request = new Pending();
        request.Completion.SetResult(true);
        IAsyncEnumerator<int> enumerator = dispatcher
            .Stream(request, dispatchToken ? source.Token : CancellationToken.None).GetAsyncEnumerator();

        (await enumerator.MoveNextAsync()).ShouldBeTrue();
        enumerator.Current.ShouldBe(7);
        await enumerator.DisposeAsync();

        request.Disposals.ShouldBe(1);
    }

    #region Helpers

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options => options.WithScopedHandlers().AddHandler<PendingHandler>());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public sealed class Pending : IStreamRequest<int>
    {
        public TaskCompletionSource<bool> Completion { get; } = new();
        public int Disposals { get; set; }
    }

    public sealed class PendingHandler : IStreamRequestHandler<Pending, int>
    {
        public IAsyncEnumerable<int> Handle(Pending request, CancellationToken cancellationToken)
            => new PendingSequence(request);
    }

    private sealed class PendingSequence(Pending request) : IAsyncEnumerable<int>
    {
        public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new PendingEnumerator(request);
    }

    private sealed class PendingEnumerator(Pending request) : IAsyncEnumerator<int>
    {
        public int Current => 7;

        public ValueTask<bool> MoveNextAsync() => new(request.Completion.Task);

        public ValueTask DisposeAsync()
        {
            request.Disposals++;
            return default;
        }
    }

    #endregion
}
