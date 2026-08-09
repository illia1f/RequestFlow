using Microsoft.Extensions.DependencyInjection;
using RequestFlow;
using RequestFlow.Tests.ValidationFixtures;

namespace RequestFlow.Tests.Unit;

public sealed class StagePipelineTests
{
    [Fact]
    public async Task Given_Open_Stage_When_Sending_Request_Then_Stage_Wraps_The_Handler()
    {
        IRequestDispatcher dispatcher = Build(o => o.AddStage(typeof(RecordingStage<,>)));

        string result = await dispatcher.SendAsync(new Ping("hi"));

        result.ShouldBe("hi");
        Trace.ShouldBe(["Recording:enter", "Recording:exit"]);
    }

    [Fact]
    public async Task Given_Two_Stages_When_Sending_Request_Then_Registration_Order_Is_Execution_Order()
    {
        IRequestDispatcher dispatcher = Build(o => o
            .AddStage(typeof(RecordingStage<,>))
            .AddStage(typeof(SecondStage<,>)));

        await dispatcher.SendAsync(new Ping("hi"));

        Trace.ShouldBe(["Recording:enter", "Second:enter", "Second:exit", "Recording:exit"]);
    }

    [Fact]
    public async Task Given_Constrained_Stage_When_Sending_A_Request_It_Excludes_Then_Stage_Does_Not_Run()
    {
        IRequestDispatcher dispatcher = Build(o => o.AddStage(typeof(TaggedOnlyStage<,>)));

        await dispatcher.SendAsync(new Ping("hi"));

        Trace.ShouldBeEmpty();
    }

    [Fact]
    public async Task Given_Constrained_Stage_When_Sending_A_Request_It_Admits_Then_Stage_Runs()
    {
        IRequestDispatcher dispatcher = Build(o => o.AddStage(typeof(TaggedOnlyStage<,>)));

        await dispatcher.SendAsync(new Tagged("hi"));

        Trace.ShouldBe(["TaggedOnly:enter", "TaggedOnly:exit"]);
    }

    [Fact]
    public async Task Given_Closed_Stage_When_Sending_Requests_Then_Only_Its_Own_Request_Is_Wrapped()
    {
        IRequestDispatcher dispatcher = Build(o => o.AddStage<PingOnlyStage>());

        await dispatcher.SendAsync(new Ping("hi"));
        await dispatcher.SendAsync(new Tagged("hi"));

        Trace.ShouldBe(["PingOnly:enter", "PingOnly:exit"]);
    }

    [Fact]
    public async Task Given_Closed_Stage_For_A_Base_Request_When_Sending_A_Derived_Request_Then_The_Stage_Runs()
    {
        IRequestDispatcher dispatcher = Build(o => o.AddStage<NotificationStage>());

        string result = await dispatcher.SendAsync(new EmailNotification("hi"));

        result.ShouldBe("hi");
        Trace.ShouldBe(["Notification:enter", "Notification:exit"]);
    }

    [Fact]
    public void Given_One_Stage_Type_With_Two_Handler_Filters_When_Resolving_Dispatcher_Then_Reports_The_Duplicate()
    {
        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            Build(o => o
                .AddStage(typeof(RecordingStage<,>), s => s.WhereHandlerImplements<IOrdersHandler>())
                .AddStage(typeof(RecordingStage<,>), s => s.WhereHandlerImplements<IBillingHandler>())));

        exception.Problems.ShouldContain(p =>
            p.Message.Contains(nameof(RecordingStage<Ping, string>)) && p.Message.Contains("more than once"));

        // The two filters here are disjoint, so the stage never actually lands in one chain
        // twice. The message states the rule instead of predicting a double run.
        exception.Problems.ShouldContain(p => p.Message.Contains("whatever each call filtered on"));
        exception.Problems.ShouldNotContain(p => p.Message.Contains("would run twice"));
    }

    [Fact]
    public async Task Given_Void_Request_With_A_Stage_When_Sending_Request_Then_Stage_Wraps_The_Void_Handler()
    {
        IRequestDispatcher dispatcher = Build(o => o.AddStage(typeof(RecordingStage<,>)));

        await dispatcher.SendAsync(new Wipe());

        Trace.ShouldBe(["Recording:enter", "Wipe:handled", "Recording:exit"]);
    }

    [Fact]
    public async Task Given_No_Stages_When_Sending_Request_Then_Response_Is_Unchanged()
    {
        IRequestDispatcher dispatcher = Build();

        string result = await dispatcher.SendAsync(new Ping("hi"));

        result.ShouldBe("hi");
        Trace.ShouldBeEmpty();
    }

    [Fact]
    public void Given_Same_Stage_Registered_By_Two_Calls_When_Resolving_Dispatcher_Then_Reports_Duplicate_Once()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<StagePipelineTests>()
            .AddStage(typeof(RecordingStage<,>)));
        services.AddRequestFlow(o => o.AddStage(typeof(RecordingStage<,>)));

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        exception.Problems.Count(p => p.Message.Contains(nameof(RecordingStage<Ping, string>))).ShouldBe(1);
    }

    [Fact]
    public async Task Given_Stage_That_Applies_To_Nothing_When_Not_Strict_Then_Dispatcher_Resolves_And_The_Stage_Never_Runs()
    {
        IRequestDispatcher dispatcher = Build(o => o.AddStage(typeof(UnreachableStage<,>)));

        string result = await dispatcher.SendAsync(new Ping("hi"));

        result.ShouldBe("hi");
        Trace.ShouldBeEmpty();
    }

    [Fact]
    public void Given_Open_Stage_And_Its_Own_Closed_Form_When_Resolving_Dispatcher_Then_Reports_The_Collision()
    {
        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            Build(o => o
                .AddStage(typeof(RecordingStage<,>))
                .AddStage<RecordingStage<Ping, string>>()));

        exception.Problems.Count(p => p.Message.Contains("resolve to")).ShouldBe(1);
        exception.Problems.ShouldContain(p => p.Message.Contains("RecordingStage") && p.Message.Contains(nameof(Ping)));
    }

    [Fact]
    public void Given_Open_Stage_And_A_Closed_Form_For_A_Base_Request_When_Resolving_Dispatcher_Then_Reports_The_Collision()
    {
        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            Build(o => o
                .AddStage(typeof(RecordingStage<,>))
                .AddStage<RecordingStage<Notification, string>>()));

        exception.Problems.Count(p => p.Message.Contains("same stage class")).ShouldBe(1);
        exception.Problems.ShouldContain(p => p.Message.Contains("RecordingStage") && p.Message.Contains(nameof(EmailNotification)));
    }

    [Fact]
    public void Given_Two_Closed_Forms_Of_One_Stage_For_Base_And_Derived_Requests_When_Resolving_Dispatcher_Then_Reports_The_Collision()
    {
        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            Build(o => o
                .AddStage<RecordingStage<Notification, string>>()
                .AddStage<RecordingStage<EmailNotification, string>>()));

        exception.Problems.Count(p => p.Message.Contains("same stage class")).ShouldBe(1);
        exception.Problems.ShouldContain(p => p.Message.Contains("RecordingStage") && p.Message.Contains(nameof(EmailNotification)));
    }

    [Fact]
    public async Task Given_Open_Response_Bound_Stage_When_Sending_Requests_Then_Only_Matching_Responses_Are_Wrapped()
    {
        IRequestDispatcher dispatcher = Build(o => o.AddStage(typeof(ResponseBoundStage<>)));

        string result = await dispatcher.SendAsync(new Ping("hi"));
        await dispatcher.SendAsync(new Wipe());

        result.ShouldBe("hi");
        Trace.ShouldBe(["ResponseBound:enter", "ResponseBound:exit", "Wipe:handled"]);
    }

    [Fact]
    public async Task Given_Void_Form_Stage_When_Sending_A_Void_Request_Then_It_Wraps_The_Void_Handler()
    {
        IRequestDispatcher dispatcher = Build(o => o.AddStage(typeof(VoidRecordingStage<>)));

        await dispatcher.SendAsync(new Wipe());

        Trace.ShouldBe(["VoidRecording:enter", "Wipe:handled", "VoidRecording:exit"]);
    }

    [Fact]
    public async Task Given_Void_Form_Stage_When_Sending_A_Request_That_Returns_A_Response_Then_It_Does_Not_Run()
    {
        IRequestDispatcher dispatcher = Build(o => o.AddStage(typeof(VoidRecordingStage<>)));

        await dispatcher.SendAsync(new Ping("hi"));

        Trace.ShouldBeEmpty();
    }

    [Fact]
    public async Task Given_Both_Stage_Forms_When_Sending_A_Void_Request_Then_Registration_Order_Is_Execution_Order()
    {
        IRequestDispatcher dispatcher = Build(o => o
            .AddStage(typeof(RecordingStage<,>))
            .AddStage(typeof(VoidRecordingStage<>)));

        await dispatcher.SendAsync(new Wipe());

        Trace.ShouldBe(
            ["Recording:enter", "VoidRecording:enter", "Wipe:handled", "VoidRecording:exit", "Recording:exit"]);
    }

    [Fact]
    public async Task Given_Stages_Added_By_Two_Calls_When_Sending_Request_Then_Execution_Order_Spans_The_Calls()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<StagePipelineTests>()
            .AddStage(typeof(RecordingStage<,>)));
        services.AddRequestFlow(o => o.AddStage(typeof(SecondStage<,>)));
        IRequestDispatcher dispatcher = services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>();

        await dispatcher.SendAsync(new Ping("hi"));

        Trace.ShouldBe(["Recording:enter", "Second:enter", "Second:exit", "Recording:exit"]);
    }

    [Fact]
    public void Given_Stage_That_Applies_To_Nothing_When_Strict_Then_Reports_The_Stage()
    {
        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            Build(o => o.AddStage(typeof(UnreachableStage<,>)).DisallowUnusedStages()));

        exception.Problems.ShouldContain(p =>
            p.Message.Contains("UnreachableStage") && p.Message.Contains("no registered request"));
    }

    [Fact]
    public void Given_Stage_Reaching_Only_The_Second_Handler_Of_One_Request_When_Strict_Then_Does_Not_Report_The_Stage()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssembly(typeof(Forked).Assembly)
            .AddStage<ForkedIntStage>()
            .DisallowUnusedStages());

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        exception.Problems.ShouldContain(p => p.Code == "RF0101" && p.Subject == typeof(Forked));
        exception.Problems.ShouldNotContain(p => p.Code == "RF0105");
    }

    [Fact]
    public void Given_Two_Declarations_Colliding_Only_On_The_Second_Handler_When_Resolving_Dispatcher_Then_Reports_The_Collision()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssembly(typeof(Forked).Assembly)
            .AddStage(typeof(IntBoundStage<>))
            .AddStage<IntBoundStage<Forked>>());

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        exception.Problems.ShouldContain(p =>
            p.Code == "RF0104" && p.Subject == typeof(IntBoundStage<>) && p.Message.Contains(nameof(Forked)));
    }

    // One closing per handler is one stage per chain, however many chains the request has.
    [Fact]
    public void Given_Two_Closings_Reaching_A_Handler_Each_When_Resolving_Dispatcher_Then_Reports_No_Collision()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssembly(typeof(Forked).Assembly)
            .AddStage<RecordingStage<Forked, string>>()
            .AddStage<RecordingStage<Forked, int>>());

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        exception.Problems.ShouldNotContain(p => p.Code == "RF0104");
    }

    #region Initialization

    // The container instantiates stages, so the trace has to be static; the constructor clears
    // it per test.
    private static readonly List<string> Trace = [];

    public StagePipelineTests()
        => Trace.Clear();

    #endregion

    #region Helpers

    private static IRequestDispatcher Build(Action<RequestFlowOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StagePipelineTests>();
            configure?.Invoke(o);
        });

        return services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>();
    }

    public interface ITag
    { }

    public interface INothingImplementsThis
    { }

    // Two handler contracts, so a stage can be filtered to one family of handlers.
    public interface IOrdersHandler
    { }

    public interface IBillingHandler
    { }

    public sealed record Ping(string Text) : IRequest<string>;

    public sealed record Tagged(string Text) : IRequest<string>, ITag;

    public sealed record Wipe : IRequest;

    // Abstract, so the scan never counts it as a request needing a handler of its own.
    public abstract record Notification(string Text) : IRequest<string>;

    public sealed record EmailNotification(string Text) : Notification(Text);

    public sealed class PingHandler : IRequestHandler<Ping, string>, IOrdersHandler
    {
        public Task<string> HandleAsync(Ping request, CancellationToken cancellationToken)
            => Task.FromResult(request.Text);
    }

    public sealed class TaggedHandler : IRequestHandler<Tagged, string>, IBillingHandler
    {
        public Task<string> HandleAsync(Tagged request, CancellationToken cancellationToken)
            => Task.FromResult(request.Text);
    }

    public sealed class EmailNotificationHandler : IRequestHandler<EmailNotification, string>
    {
        public Task<string> HandleAsync(EmailNotification request, CancellationToken cancellationToken)
            => Task.FromResult(request.Text);
    }

    public sealed class WipeHandler : IRequestHandler<Wipe>
    {
        public Task HandleAsync(Wipe request, CancellationToken cancellationToken)
        {
            Trace.Add("Wipe:handled");
            return Task.CompletedTask;
        }
    }

    // Declared closed over Forked's int contract, so it reaches ForkedIntHandler and not
    // ForkedStringHandler.
    public sealed class ForkedIntStage : IRequestStage<Forked, int>
    {
        public Task<int> HandleAsync(
            Forked request, Continuation<int> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    // Bound to the int contract by its constraint, so on Forked it reaches ForkedIntHandler only.
    public sealed class IntBoundStage<TRequest> : IRequestStage<TRequest, int>
        where TRequest : IRequest<int>
    {
        public Task<int> HandleAsync(
            TRequest request, Continuation<int> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    public sealed class RecordingStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(
            TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
        {
            Trace.Add("Recording:enter");
            TResponse response = await next.InvokeAsync();
            Trace.Add("Recording:exit");
            return response;
        }
    }

    public sealed class SecondStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(
            TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
        {
            Trace.Add("Second:enter");
            TResponse response = await next.InvokeAsync();
            Trace.Add("Second:exit");
            return response;
        }
    }

    public sealed class TaggedOnlyStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>, ITag
    {
        public async Task<TResponse> HandleAsync(
            TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
        {
            Trace.Add("TaggedOnly:enter");
            TResponse response = await next.InvokeAsync();
            Trace.Add("TaggedOnly:exit");
            return response;
        }
    }

    public sealed class UnreachableStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>, INothingImplementsThis
    {
        public Task<TResponse> HandleAsync(
            TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
        {
            Trace.Add("Unreachable:enter");
            return next.InvokeAsync();
        }
    }

    // One type parameter with a fixed response type, the shape Result-returning codebases use.
    public sealed class ResponseBoundStage<TRequest> : IRequestStage<TRequest, string>
        where TRequest : IRequest<string>
    {
        public async Task<string> HandleAsync(
            TRequest request, Continuation<string> next, CancellationToken cancellationToken)
        {
            Trace.Add("ResponseBound:enter");
            string response = await next.InvokeAsync();
            Trace.Add("ResponseBound:exit");
            return response;
        }
    }

    public sealed class VoidRecordingStage<TRequest> : IRequestStage<TRequest>
        where TRequest : IRequest
    {
        public async Task HandleAsync(TRequest request, Continuation next, CancellationToken cancellationToken)
        {
            Trace.Add("VoidRecording:enter");
            await next.InvokeAsync();
            Trace.Add("VoidRecording:exit");
        }
    }

    // Declared for the base request, so contravariance on TRequest is the only thing that can
    // reach EmailNotification.
    public sealed class NotificationStage : IRequestStage<Notification, string>
    {
        public async Task<string> HandleAsync(
            Notification request, Continuation<string> next, CancellationToken cancellationToken)
        {
            Trace.Add("Notification:enter");
            string response = await next.InvokeAsync();
            Trace.Add("Notification:exit");
            return response;
        }
    }

    public sealed class PingOnlyStage : IRequestStage<Ping, string>
    {
        public async Task<string> HandleAsync(
            Ping request, Continuation<string> next, CancellationToken cancellationToken)
        {
            Trace.Add("PingOnly:enter");
            string response = await next.InvokeAsync();
            Trace.Add("PingOnly:exit");
            return response;
        }
    }

    #endregion
}
