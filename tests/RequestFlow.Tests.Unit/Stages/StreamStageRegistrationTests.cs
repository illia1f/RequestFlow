using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Stages;

public sealed class StreamStageRegistrationTests
{
    [Fact]
    public async Task Given_A_Stream_Stage_Declared_By_A_Later_Call_When_Enumerating_Then_It_Reaches_The_Earlier_Handler()
    {
        Trace.Clear();
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<StreamStageRegistrationTests>());
        services.AddRequestFlow(o => o.AddStreamStage<MarkingStage>());
        IStreamDispatcher sut = services.BuildServiceProvider().CreateScope().ServiceProvider
            .GetRequiredService<IStreamDispatcher>();

        await sut.Stream(new Beat()).CollectAsync();

        Trace.ShouldBe(["stage", "handler"]);
    }

    [Fact]
    public async Task Given_A_Stream_Stage_When_A_Task_Request_Is_Dispatched_Then_The_Stage_Does_Not_Run()
    {
        Trace.Clear();
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StreamStageRegistrationTests>();
            o.AddStreamStage(typeof(OpenMarkingStage<,>));
        });
        IServiceProvider provider = services.BuildServiceProvider().CreateScope().ServiceProvider;

        await provider.GetRequiredService<IRequestDispatcher>().SendAsync(new Ask());

        Trace.ShouldBeEmpty();

        // Proves the stage is live, not merely absent: the same provider's stream chain still runs it.
        await provider.GetRequiredService<IStreamDispatcher>().Stream(new Beat()).CollectAsync();

        Trace.ShouldBe(["stream stage", "handler"]);
    }

    [Fact]
    public async Task Given_A_Request_Stage_When_A_Stream_Request_Is_Dispatched_Then_The_Stage_Does_Not_Run()
    {
        Trace.Clear();
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StreamStageRegistrationTests>();
            o.AddStage(typeof(OpenTaskStage<,>));
        });
        IServiceProvider provider = services.BuildServiceProvider().CreateScope().ServiceProvider;

        await provider.GetRequiredService<IStreamDispatcher>().Stream(new Beat()).CollectAsync();

        Trace.ShouldBe(["handler"]);

        // Proves the stage is live, not merely absent: the same provider's task chain still runs it.
        await provider.GetRequiredService<IRequestDispatcher>().SendAsync(new Ask());

        Trace.ShouldBe(["handler", "task stage"]);
    }

    [Fact]
    public async Task Given_A_Closed_Stream_Stage_When_The_Assembly_Also_Holds_Task_Handlers_Then_The_Freeze_Succeeds()
    {
        Trace.Clear();
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StreamStageRegistrationTests>();
            o.AddStreamStage<MarkingStage>();
        });
        IStreamDispatcher sut = services.BuildServiceProvider().CreateScope().ServiceProvider
            .GetRequiredService<IStreamDispatcher>();

        await sut.Stream(new Beat()).CollectAsync();

        Trace.ShouldBe(["stage", "handler"]);
    }

    [Fact]
    public void Given_A_Stream_Stage_That_Does_Not_Implement_The_Contract_When_Freezing_Then_Reports_RF0011()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StreamStageRegistrationTests>();
            o.AddStreamStage<NotAStage>();
        });

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => services.BuildServiceProvider().GetRequiredService<IStreamDispatcher>());

        exception.Problems.ShouldContain(p =>
            p.Code == "RF0011"
            && p.Subject == typeof(NotAStage)
            && p.Message.Contains("IStreamRequestStage<TRequest, TItem>")
            && p.Message.Contains("AddStreamStage"));
    }

    [Fact]
    public void Given_One_Stage_Type_Declared_In_Both_Families_When_Freezing_Then_Reports_RF0103()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StreamStageRegistrationTests>();
            o.AddStreamStage<BothStage>();
            o.AddStage<BothStage>();
        });

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => services.BuildServiceProvider().GetRequiredService<IStreamDispatcher>());

        exception.Problems.ShouldContain(p => p.Code == "RF0103" && p.Subject == typeof(BothStage));
    }

    [Fact]
    public void Given_An_Applied_Stream_Stage_When_Unused_Stages_Are_Disallowed_Then_Nothing_Is_Reported()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StreamStageRegistrationTests>();
            o.AddStreamStage<MarkingStage>();
            o.DisallowUnusedStages();
        });

        Should.NotThrow(() => services.BuildServiceProvider().GetRequiredService<IStreamDispatcher>());
    }

    #region Helpers

    private static readonly List<string> Trace = [];

    public sealed record Beat : IStreamRequest<int>;

    public sealed record Ask : IRequest<string>;

    public sealed class BeatHandler : IStreamRequestHandler<Beat, int>
    {
        public async IAsyncEnumerable<int> Handle(
            Beat request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Trace.Add("handler");

            await Task.Yield();
            yield return 1;
        }
    }

    public sealed class AskHandler : IRequestHandler<Ask, string>
    {
        public Task<string> HandleAsync(Ask request, CancellationToken cancellationToken)
            => Task.FromResult("answered");
    }

    public sealed class MarkingStage : IStreamRequestStage<Beat, int>
    {
        public IAsyncEnumerable<int> Handle(
            Beat request, StreamContinuation<int> next, CancellationToken cancellationToken)
        {
            Trace.Add("stage");
            return next.Invoke(cancellationToken);
        }
    }

    public sealed class OpenMarkingStage<TRequest, TItem> : IStreamRequestStage<TRequest, TItem>
        where TRequest : IStreamRequest<TItem>
    {
        public IAsyncEnumerable<TItem> Handle(
            TRequest request, StreamContinuation<TItem> next, CancellationToken cancellationToken)
        {
            Trace.Add("stream stage");
            return next.Invoke(cancellationToken);
        }
    }

    public sealed class OpenTaskStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
        {
            Trace.Add("task stage");
            return next.InvokeAsync(cancellationToken);
        }
    }

    public sealed class NotAStage
    { }

    public sealed class BothStage : IStreamRequestStage<Beat, int>, IRequestStage<Ask, string>
    {
        public IAsyncEnumerable<int> Handle(
            Beat request, StreamContinuation<int> next, CancellationToken cancellationToken)
            => next.Invoke(cancellationToken);

        public Task<string> HandleAsync(
            Ask request, Continuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    #endregion
}
