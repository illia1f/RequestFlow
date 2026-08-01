using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class StageSemanticsTests
{
    [Fact]
    public async Task Given_Stage_That_Throws_When_Sending_Request_Then_Exception_Propagates_Unwrapped()
    {
        IRequestDispatcher dispatcher = Build(o => o.AddStage(typeof(ThrowingStage<,>)));

        var exception = await Should.ThrowAsync<InvalidTimeZoneException>(() => dispatcher.SendAsync(new Ping("hi")));

        exception.Message.ShouldBe("from stage");
    }

    [Fact]
    public async Task Given_Handler_That_Throws_Behind_Two_Stages_When_Sending_Request_Then_Exception_Propagates_Unwrapped()
    {
        IRequestDispatcher dispatcher = Build(o => o
            .AddStage(typeof(PassThroughStage<,>))
            .AddStage(typeof(SecondPassThroughStage<,>)));

        var exception = await Should.ThrowAsync<NotSupportedException>(() => dispatcher.SendAsync(new Boom()));

        exception.Message.ShouldBe("from handler");
    }

    [Fact]
    public async Task Given_Cancelled_Token_When_Sending_Request_Then_Stage_And_Handler_See_The_Cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        IRequestDispatcher dispatcher = Build(o => o.AddStage(typeof(TokenForwardingStage<,>)));

        string result = await dispatcher.SendAsync(new Ping("hi"), cts.Token);

        result.ShouldBe("cancelled");
        Trace.ShouldBe(["Token:cancelled"]);
    }

    [Fact]
    public async Task Given_Cancelled_Token_When_Sending_A_Void_Request_Then_The_Stage_And_The_Void_Handler_See_It()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        IRequestDispatcher dispatcher = Build(o => o.AddStage(typeof(TokenForwardingStage<,>)));

        await dispatcher.SendAsync(new Wipe(), cts.Token);

        Trace.ShouldBe(["Token:cancelled"]);
        WipeHandler.SawCancellation.ShouldBeTrue();
    }

    [Fact]
    public async Task Given_Stage_That_Throws_After_Next_When_Sending_Request_Then_Exception_Propagates_Unwrapped()
    {
        IRequestDispatcher dispatcher = Build(o => o
            .AddStage(typeof(ThrowAfterNextStage<,>))
            .AddStage(typeof(TracingStage<,>)));

        var exception = await Should.ThrowAsync<TimeoutException>(() => dispatcher.SendAsync(new Ping("hi")));

        exception.Message.ShouldBe("after next");
        Trace.ShouldBe(["Tracing:enter", "Tracing:exit"]);
    }

    [Fact]
    public async Task Given_Stage_That_Awaits_Before_Calling_Next_When_Sending_Request_Then_Response_Returns()
    {
        IRequestDispatcher dispatcher = Build(o => o.AddStage(typeof(AwaitBeforeNextStage<,>)));

        string result = await dispatcher.SendAsync(new Ping("hi"));

        result.ShouldBe("hi");
        Trace.ShouldBe(["Await:enter", "Await:exit"]);
    }

    [Fact]
    public async Task Given_Outer_Stage_That_Calls_Next_Twice_When_Sending_Request_Then_The_Inner_Stage_Runs_Twice()
    {
        IRequestDispatcher dispatcher = Build(o => o
            .AddStage(typeof(DoubleNextStage<,>))
            .AddStage(typeof(TracingStage<,>)));

        await dispatcher.SendAsync(new Ping("hi"));

        Trace.ShouldBe(["Tracing:enter", "Tracing:exit", "Tracing:enter", "Tracing:exit"]);
    }

    [Fact]
    public async Task Given_Failing_Handler_When_Outer_Stage_Retries_When_Sending_Request_Then_The_Second_Attempt_Runs_The_Inner_Chain()
    {
        IRequestDispatcher dispatcher = Build(o => o
            .AddStage(typeof(RetryOnceStage<,>))
            .AddStage(typeof(TracingStage<,>)));

        string result = await dispatcher.SendAsync(new Counted());

        result.ShouldBe("2");
        Trace.ShouldBe(["Tracing:enter", "Tracing:enter", "Tracing:exit"]);
    }

    [Fact]
    public async Task Given_Nested_Send_Inside_A_Handler_When_Sending_Request_Then_The_Inner_Request_Runs_Its_Own_Chain()
    {
        IRequestDispatcher dispatcher = Build(o => o.AddStage(typeof(CountingStage<,>)));

        await dispatcher.SendAsync(new Outer());

        CountingStage<Outer, string>.Entries.ShouldBe(1);
        CountingStage<Inner, string>.Entries.ShouldBe(1);
    }

    [Fact]
    public async Task Given_Stage_On_A_Void_Request_When_Sending_Request_Then_Returned_Task_Is_Already_Completed()
    {
        IRequestDispatcher dispatcher = Build(o => o.AddStage(typeof(TracingStage<,>)));

        Task task = dispatcher.SendAsync(new Wipe());

        task.IsCompleted.ShouldBeTrue();
        Trace.ShouldBe(["Tracing:enter", "Tracing:exit"]);
        await task;
    }

    #region Initialization

    // The container instantiates stages, so the trace and counters have to be static; the
    // constructor clears them per test. Each closed form of a generic stage carries its own.
    private static readonly List<string> Trace = [];

    public StageSemanticsTests()
    {
        Trace.Clear();
        WipeHandler.SawCancellation = false;
        CountedHandler.Calls = 0;
        CountingStage<Outer, string>.Entries = 0;
        CountingStage<Inner, string>.Entries = 0;
    }

    #endregion

    #region Helpers

    private static IRequestDispatcher Build(Action<RequestFlowOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StageSemanticsTests>();
            configure?.Invoke(o);
        });

        return services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>();
    }

    public sealed record Ping(string Text) : IRequest<string>;

    public sealed record Boom : IRequest<string>;

    public sealed record Counted : IRequest<string>;

    public sealed record Outer : IRequest<string>;

    public sealed record Inner : IRequest<string>;

    public sealed record Wipe : IRequest;

    public sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, CancellationToken cancellationToken)
            => Task.FromResult(cancellationToken.IsCancellationRequested ? "cancelled" : request.Text);
    }

    public sealed class BoomHandler : IRequestHandler<Boom, string>
    {
        public Task<string> HandleAsync(Boom request, CancellationToken cancellationToken)
            => throw new NotSupportedException("from handler");
    }

    // Faults the first attempt and then succeeds, so a retrying stage has something to retry.
    public sealed class CountedHandler : IRequestHandler<Counted, string>
    {
        public static int Calls;

        public Task<string> HandleAsync(Counted request, CancellationToken cancellationToken)
        {
            Calls++;

            return Calls == 1
                ? Task.FromException<string>(new InvalidOperationException("first attempt fails"))
                : Task.FromResult(Calls.ToString());
        }
    }

    public sealed class OuterHandler(IRequestDispatcher dispatcher) : IRequestHandler<Outer, string>
    {
        public Task<string> HandleAsync(Outer request, CancellationToken cancellationToken)
            => dispatcher.SendAsync(new Inner(), cancellationToken);
    }

    public sealed class InnerHandler : IRequestHandler<Inner, string>
    {
        public Task<string> HandleAsync(Inner request, CancellationToken cancellationToken)
            => Task.FromResult("inner");
    }

    public sealed class WipeHandler : IRequestHandler<Wipe>
    {
        public static bool SawCancellation;

        public Task HandleAsync(Wipe request, CancellationToken cancellationToken)
        {
            SawCancellation = cancellationToken.IsCancellationRequested;
            return Task.CompletedTask;
        }
    }

    public sealed class ThrowAfterNextStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(
            TRequest request, StageDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            await next();
            throw new TimeoutException("after next");
        }
    }

    public sealed class ThrowingStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request, StageDelegate<TResponse> next, CancellationToken cancellationToken)
            => throw new InvalidTimeZoneException("from stage");
    }

    public sealed class PassThroughStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request, StageDelegate<TResponse> next, CancellationToken cancellationToken)
            => next();
    }

    public sealed class SecondPassThroughStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request, StageDelegate<TResponse> next, CancellationToken cancellationToken)
            => next();
    }

    public sealed class TokenForwardingStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request, StageDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                Trace.Add("Token:cancelled");

            return next();
        }
    }

    // Suspends on work of its own before delegating, the shape of a validation or caching stage.
    public sealed class AwaitBeforeNextStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(
            TRequest request, StageDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            await Task.Yield();
            Trace.Add("Await:enter");
            TResponse response = await next();
            Trace.Add("Await:exit");
            return response;
        }
    }

    public sealed class TracingStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(
            TRequest request, StageDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            Trace.Add("Tracing:enter");
            TResponse response = await next();
            Trace.Add("Tracing:exit");
            return response;
        }
    }

    public sealed class DoubleNextStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(
            TRequest request, StageDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            await next();
            return await next();
        }
    }

    public sealed class RetryOnceStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(
            TRequest request, StageDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            try
            {
                return await next();
            }
            catch (InvalidOperationException)
            {
                return await next();
            }
        }
    }

    public sealed class CountingStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public static int Entries;

        public Task<TResponse> HandleAsync(
            TRequest request, StageDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            Entries++;
            return next();
        }
    }

    #endregion
}
