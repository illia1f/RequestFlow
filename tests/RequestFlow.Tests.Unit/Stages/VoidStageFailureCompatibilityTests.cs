using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow.Tests.Unit;

public sealed class VoidStageFailureCompatibilityTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Given_A_Cancellation_Fault_In_A_Void_Pipeline_When_Sending_Then_The_Outer_Stage_And_Caller_Observe_Cancellation(
        bool failInStage, bool completesLater)
    {
        using ServiceProvider provider = Build();
        using IServiceScope scope = provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var failure = new OperationCanceledException(cancellation.Token);
        var request = new Signal(failInStage);
        if (!completesLater)
            request.Completion.SetException(failure);

        Task operation = dispatcher.SendAsync(request);
        if (completesLater)
            request.Completion.SetException(failure);
        Exception? caught = null;
        try { await operation; }
        catch (Exception exception) { caught = exception; }

        caught.ShouldBeSameAs(failure);
        operation.IsCanceled.ShouldBeTrue();
        request.Inner!.IsCanceled.ShouldBeTrue();
        request.FinallyCalls.ShouldBe(1);
    }

    #region Helpers

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options => options.WithScopedHandlers()
            .AddHandler<SignalHandler>()
            .AddStage(typeof(ObservationStage<,>))
            .AddStage<FailureStage>());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public sealed class Signal(bool failInStage) : IRequest
    {
        public bool FailInStage { get; } = failInStage;
        public TaskCompletionSource<object?> Completion { get; } = new();
        public Task? Inner { get; set; }
        public int FinallyCalls { get; set; }
    }

    public sealed class SignalHandler : IRequestHandler<Signal>
    {
        public Task HandleAsync(Signal request, CancellationToken cancellationToken)
            => request.Completion.Task;
    }

    public sealed class FailureStage : IRequestStage<Signal>
    {
        public Task HandleAsync(Signal request, Continuation next, CancellationToken cancellationToken)
            => request.FailInStage ? request.Completion.Task : next.InvokeAsync();
    }

    public sealed class ObservationStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
        {
            var signal = (Signal)(object)request;
            Task<TResponse> inner = next.InvokeAsync();
            signal.Inner = inner;
            try { return await inner.ConfigureAwait(false); }
            finally { signal.FinallyCalls++; }
        }
    }

    #endregion
}
