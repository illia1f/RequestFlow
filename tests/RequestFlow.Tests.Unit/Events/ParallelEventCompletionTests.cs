namespace RequestFlow.Tests.Unit.Events;

public sealed class ParallelEventCompletionTests
{
    [Fact]
    public async Task Given_One_Pending_Handler_Among_Synchronous_Handlers_When_Publishing_Then_All_Start_Before_Waiting()
    {
        var gate = new TaskCompletionSource<object?>();
        var calls = new List<int>();
        ParallelEventPlan plan = CreatePlan((index, _) =>
        {
            calls.Add(index);
            return index == 1 ? gate.Task : Task.CompletedTask;
        });

        Task publish = plan.ExecuteAsync(new Notice(), _services, default);

        calls.ShouldBe([0, 1, 2], ignoreOrder: false);
        publish.IsCompleted.ShouldBeFalse();
        gate.SetResult(null);
        await publish;
    }

    [Fact]
    public async Task Given_Concurrent_Publications_When_One_Completes_Synchronously_Then_The_Other_Keeps_Its_Pending_Handler()
    {
        var gate = new TaskCompletionSource<object?>();
        var pending = new Notice(gate.Task);
        var entries = new HandlerEntry[]
        {
            (_, _, _) => Task.CompletedTask,
            (_, value, _) => ((Notice)value).Operation,
            (_, _, _) => Task.CompletedTask,
        };
        ParallelEventPlan plan = CreatePlan(entries);

        Task first = plan.ExecuteAsync(pending, _services, default);
        Task second = plan.ExecuteAsync(new Notice(), _services, default);

        await second;
        first.IsCompleted.ShouldBeFalse();
        gate.SetResult(null);
        await first;
    }

    [Fact]
    public async Task Given_A_Synchronous_Handler_Cancels_The_Publishing_Token_When_Publishing_Then_All_Entries_Still_Start()
    {
        using var source = new CancellationTokenSource();
        var calls = new List<int>();
        ParallelEventPlan plan = CreatePlan((index, token) =>
        {
            token.ShouldBe(source.Token);
            calls.Add(index);
            if (index == 0)
                source.Cancel();
            return Task.CompletedTask;
        });

        await plan.ExecuteAsync(new Notice(), _services, source.Token);

        calls.ShouldBe([0, 1, 2], ignoreOrder: false);
    }

    #region Initialization

    private readonly IServiceProvider _services = Substitute.For<IServiceProvider>();

    #endregion

    #region Helpers

    private static readonly Type[] HandlerTypes = [typeof(AlphaHandler), typeof(BravoHandler), typeof(CharlieHandler)];

    private static ParallelEventPlan CreatePlan(Func<int, CancellationToken, Task> start)
    {
        var entries = new HandlerEntry[HandlerTypes.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            int index = i;
            entries[i] = (_, _, token) => start(index, token);
        }
        return CreatePlan(entries);
    }

    private static ParallelEventPlan CreatePlan(HandlerEntry[] entries)
    {
        var handlers = new EventHandlerModel[entries.Length];
        for (int i = 0; i < handlers.Length; i++)
            handlers[i] = new EventHandlerModel(HandlerTypes[i], typeof(Notice), RequestFlowLifetime.Singleton);
        return new ParallelEventPlan(new EventModel(typeof(Notice), handlers), entries);
    }

    private sealed class Notice(Task? operation = null) : IEvent
    {
        public Task Operation { get; } = operation ?? Task.CompletedTask;
    }

    private sealed class AlphaHandler : IEventHandler<Notice>
    {
        public Task HandleAsync(Notice @event, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class BravoHandler : IEventHandler<Notice>
    {
        public Task HandleAsync(Notice @event, CancellationToken cancellationToken) => @event.Operation;
    }

    private sealed class CharlieHandler : IEventHandler<Notice>
    {
        public Task HandleAsync(Notice @event, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    #endregion
}
