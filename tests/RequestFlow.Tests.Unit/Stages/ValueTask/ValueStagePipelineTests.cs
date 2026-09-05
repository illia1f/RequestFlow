using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class ValueStagePipelineTests
{
    [Fact]
    public async Task Given_Open_Typed_Value_Stage_When_Dispatching_Task_And_Value_Requests_Then_It_Wraps_Only_Value_Handler()
    {
        using ServiceProvider provider = BuildProvider(options =>
        {
            options.AddHandler<TaskProbeHandler>();
            options.AddHandler<ValueProbeHandler>();
            options.AddValueStage(typeof(OpenValueStage<,>));
        });
        using IServiceScope scope = provider.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IRequestDispatcher>()
            .SendAsync(new TaskProbe());
        await scope.ServiceProvider.GetRequiredService<IValueRequestDispatcher>()
            .SendAsync(new ValueProbe());

        Trace.ShouldBe(["task handler", "value stage", "value handler"]);
    }

    [Fact]
    public async Task Given_Task_And_Stream_Stages_When_Dispatching_A_Value_Request_Then_Neither_Enters_The_Value_Chain()
    {
        IValueRequestDispatcher dispatcher = BuildDispatcher(options =>
        {
            options.AddHandler<ValueProbeHandler>();
            options.AddStage<TaskOnlyStage>();
            options.AddStreamStage<StreamOnlyStage>();
        });

        await dispatcher.SendAsync(new ValueProbe());

        Trace.ShouldBe(["value handler"]);
    }

    [Fact]
    public async Task Given_Two_Value_Stages_When_Dispatching_Then_They_Execute_Outermost_First()
    {
        IValueRequestDispatcher dispatcher = BuildDispatcher(options =>
        {
            options.AddHandler<OrderedHandler>();
            options.AddValueStage<OuterStage>();
            options.AddValueStage<InnerStage>();
        });

        string result = await dispatcher.SendAsync(new Ordered());

        result.ShouldBe("ordered");
        Trace.ShouldBe(["outer", "inner", "handler"]);
    }

    [Fact]
    public async Task Given_Constrained_Value_Stage_When_Dispatching_Included_And_Excluded_Requests_Then_It_Runs_Only_For_The_Included_Request()
    {
        IValueRequestDispatcher dispatcher = BuildDispatcher(options =>
        {
            options.AddHandler<AuditedValueHandler>();
            options.AddHandler<PlainValueHandler>();
            options.AddValueStage(typeof(AuditedValueStage<,>));
        });

        await dispatcher.SendAsync(new PlainValue());
        await dispatcher.SendAsync(new AuditedValue());

        Trace.ShouldBe(["plain handler", "audited stage", "audited handler"]);
    }

    [Fact]
    public async Task Given_Closed_Value_Stage_For_Base_Request_When_Dispatching_Derived_Request_Then_It_Runs()
    {
        IValueRequestDispatcher dispatcher = BuildDispatcher(options =>
        {
            options.AddHandler<DerivedValueHandler>();
            options.AddValueStage<BaseValueStage>();
        });

        string result = await dispatcher.SendAsync(new DerivedValue());

        result.ShouldBe("derived");
        Trace.ShouldBe(["base stage", "derived handler"]);
    }

    [Fact]
    public async Task Given_Value_Stage_That_Short_Circuits_When_Dispatching_Then_Handler_Is_Not_Resolved()
    {
        IValueRequestDispatcher dispatcher = BuildDispatcher(options =>
        {
            options.AddHandler<ShortCircuitedHandler>();
            options.AddValueStage<ShortCircuitStage>();
        });

        string result = await dispatcher.SendAsync(new ShortCircuited());

        result.ShouldBe("stopped");
        ShortCircuitedHandler.Constructions.ShouldBe(0);
    }

    [Fact]
    public async Task Given_Plain_Void_Value_Stage_When_Dispatching_Then_It_Wraps_Plain_Void_Handler()
    {
        IValueRequestDispatcher dispatcher = BuildDispatcher(options =>
        {
            options.AddHandler<VoidValueHandler>();
            options.AddValueStage<PlainVoidStage>();
        });

        await dispatcher.SendAsync(new VoidValue());

        Trace.ShouldBe(["plain stage", "void handler"]);
    }

    [Fact]
    public async Task Given_Typed_And_Plain_Void_Value_Stages_When_Dispatching_Then_They_Share_One_Direct_Void_Chain()
    {
        IValueRequestDispatcher dispatcher = BuildDispatcher(options =>
        {
            options.AddHandler<VoidValueHandler>();
            options.AddValueStage<TypedVoidStage>();
            options.AddValueStage<PlainVoidStage>();
        });

        await dispatcher.SendAsync(new VoidValue());

        Trace.ShouldBe(["typed stage", "plain stage", "void handler"]);
    }

    [Fact]
    public async Task Given_Value_Stages_From_Two_Add_Request_Flow_Calls_When_Dispatching_Then_Global_Order_Is_Preserved()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options =>
        {
            options.AddHandler<OrderedHandler>();
            options.AddValueStage<OuterStage>();
        });
        services.AddRequestFlow(options => options.AddValueStage<InnerStage>());
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IValueRequestDispatcher>()
            .SendAsync(new Ordered());

        Trace.ShouldBe(["outer", "inner", "handler"]);
    }

    [Fact]
    public async Task Given_Value_Stage_Filter_When_Dispatching_Then_It_Includes_Only_The_Named_Handler_Contract()
    {
        IValueRequestDispatcher dispatcher = BuildDispatcher(options =>
        {
            options.AddHandler<FilteredHandler>();
            options.AddHandler<UnfilteredHandler>();
            options.AddValueStage(
                typeof(FilteredStage<,>),
                stage => stage.WhereHandlerImplements<IFilteredHandler>());
        });

        await dispatcher.SendAsync(new Unfiltered());
        await dispatcher.SendAsync(new Filtered());

        Trace.ShouldBe(["unfiltered handler", "filtered stage", "filtered handler"]);
    }

    [Fact]
    public void Given_Stage_Registered_Through_Add_Stage_And_Add_Value_Stage_When_Freezing_Then_RF0103_Names_Both_Calls()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options =>
        {
            options.AddStage<TaskAndValueStage>();
            options.AddValueStage<TaskAndValueStage>();
        });

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IValueRequestDispatcher>());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0103");
        problem.Subject.ShouldBe(typeof(TaskAndValueStage));
        problem.Message.ShouldContain("AddStage and AddValueStage");
    }

    #region Initialization

    private static readonly List<string> Trace = [];

    public ValueStagePipelineTests()
    {
        Trace.Clear();
        ShortCircuitedHandler.Constructions = 0;
    }

    #endregion

    #region Helpers

    private static IValueRequestDispatcher BuildDispatcher(Action<RequestFlowOptions> configure)
        => BuildProvider(configure).CreateScope().ServiceProvider
            .GetRequiredService<IValueRequestDispatcher>();

    private static ServiceProvider BuildProvider(Action<RequestFlowOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(configure);
        return services.BuildServiceProvider();
    }

    private sealed record TaskProbe : IRequest<string>;

    private sealed record ValueProbe : IValueRequest<string>;

    private abstract record TaskOnlyRequest : IRequest<string>;

    private abstract record StreamOnlyRequest : IStreamRequest<string>;

    private sealed record Ordered : IValueRequest<string>;

    private interface IAuditedValueRequest<TResponse> : IValueRequest<TResponse>
    { }

    private sealed record AuditedValue : IAuditedValueRequest<string>;

    private sealed record PlainValue : IValueRequest<string>;

    private abstract record BaseValue : IValueRequest<string>;

    private sealed record DerivedValue : BaseValue;

    private sealed record ShortCircuited : IValueRequest<string>;

    private sealed record VoidValue : IValueRequest;

    private sealed record Filtered : IValueRequest<string>;

    private sealed record Unfiltered : IValueRequest<string>;

    private abstract record TaskDual : IRequest<string>;

    private abstract record ValueDual : IValueRequest<string>;

    private sealed class TaskProbeHandler : IRequestHandler<TaskProbe, string>
    {
        public Task<string> HandleAsync(TaskProbe request, CancellationToken cancellationToken)
        {
            Trace.Add("task handler");
            return Task.FromResult("task");
        }
    }

    private sealed class ValueProbeHandler : IValueRequestHandler<ValueProbe, string>
    {
        public ValueTask<string> HandleAsync(ValueProbe request, CancellationToken cancellationToken)
        {
            Trace.Add("value handler");
            return new ValueTask<string>("value");
        }
    }

    private sealed class OrderedHandler : IValueRequestHandler<Ordered, string>
    {
        public ValueTask<string> HandleAsync(Ordered request, CancellationToken cancellationToken)
        {
            Trace.Add("handler");
            return new ValueTask<string>("ordered");
        }
    }

    private sealed class AuditedValueHandler : IValueRequestHandler<AuditedValue, string>
    {
        public ValueTask<string> HandleAsync(AuditedValue request, CancellationToken cancellationToken)
        {
            Trace.Add("audited handler");
            return new ValueTask<string>("audited");
        }
    }

    private sealed class PlainValueHandler : IValueRequestHandler<PlainValue, string>
    {
        public ValueTask<string> HandleAsync(PlainValue request, CancellationToken cancellationToken)
        {
            Trace.Add("plain handler");
            return new ValueTask<string>("plain");
        }
    }

    private sealed class DerivedValueHandler : IValueRequestHandler<DerivedValue, string>
    {
        public ValueTask<string> HandleAsync(DerivedValue request, CancellationToken cancellationToken)
        {
            Trace.Add("derived handler");
            return new ValueTask<string>("derived");
        }
    }

    private sealed class ShortCircuitedHandler : IValueRequestHandler<ShortCircuited, string>
    {
        public static int Constructions;

        public ShortCircuitedHandler()
            => Constructions++;

        public ValueTask<string> HandleAsync(
            ShortCircuited request,
            CancellationToken cancellationToken)
            => new("handler");
    }

    private sealed class VoidValueHandler : IValueRequestHandler<VoidValue>
    {
        public ValueTask HandleAsync(VoidValue request, CancellationToken cancellationToken)
        {
            Trace.Add("void handler");
            return default;
        }
    }

    private interface IFilteredHandler
    { }

    private sealed class FilteredHandler : IValueRequestHandler<Filtered, string>, IFilteredHandler
    {
        public ValueTask<string> HandleAsync(Filtered request, CancellationToken cancellationToken)
        {
            Trace.Add("filtered handler");
            return new ValueTask<string>("filtered");
        }
    }

    private sealed class UnfilteredHandler : IValueRequestHandler<Unfiltered, string>
    {
        public ValueTask<string> HandleAsync(Unfiltered request, CancellationToken cancellationToken)
        {
            Trace.Add("unfiltered handler");
            return new ValueTask<string>("unfiltered");
        }
    }

    private sealed class OpenValueStage<TRequest, TResponse> : IValueRequestStage<TRequest, TResponse>
        where TRequest : IValueRequest<TResponse>
    {
        public ValueTask<TResponse> HandleAsync(
            TRequest request,
            ValueContinuation<TResponse> next,
            CancellationToken cancellationToken)
        {
            Trace.Add("value stage");
            return next.InvokeAsync(cancellationToken);
        }
    }

    private sealed class TaskOnlyStage : IRequestStage<TaskOnlyRequest, string>
    {
        public Task<string> HandleAsync(
            TaskOnlyRequest request,
            Continuation<string> next,
            CancellationToken cancellationToken)
        {
            Trace.Add("task stage");
            return next.InvokeAsync(cancellationToken);
        }
    }

    private sealed class StreamOnlyStage : IStreamRequestStage<StreamOnlyRequest, string>
    {
        public IAsyncEnumerable<string> Handle(
            StreamOnlyRequest request,
            StreamContinuation<string> next,
            CancellationToken cancellationToken)
        {
            Trace.Add("stream stage");
            return next.Invoke(cancellationToken);
        }
    }

    private sealed class OuterStage : IValueRequestStage<Ordered, string>
    {
        public ValueTask<string> HandleAsync(
            Ordered request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken)
        {
            Trace.Add("outer");
            return next.InvokeAsync(cancellationToken);
        }
    }

    private sealed class InnerStage : IValueRequestStage<Ordered, string>
    {
        public ValueTask<string> HandleAsync(
            Ordered request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken)
        {
            Trace.Add("inner");
            return next.InvokeAsync(cancellationToken);
        }
    }

    private sealed class AuditedValueStage<TRequest, TResponse> : IValueRequestStage<TRequest, TResponse>
        where TRequest : IAuditedValueRequest<TResponse>
    {
        public ValueTask<TResponse> HandleAsync(
            TRequest request,
            ValueContinuation<TResponse> next,
            CancellationToken cancellationToken)
        {
            Trace.Add("audited stage");
            return next.InvokeAsync(cancellationToken);
        }
    }

    private sealed class BaseValueStage : IValueRequestStage<BaseValue, string>
    {
        public ValueTask<string> HandleAsync(
            BaseValue request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken)
        {
            Trace.Add("base stage");
            return next.InvokeAsync(cancellationToken);
        }
    }

    private sealed class ShortCircuitStage : IValueRequestStage<ShortCircuited, string>
    {
        public ValueTask<string> HandleAsync(
            ShortCircuited request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken)
            => new("stopped");
    }

    private sealed class PlainVoidStage : IValueRequestStage<VoidValue>
    {
        public ValueTask HandleAsync(
            VoidValue request,
            ValueContinuation next,
            CancellationToken cancellationToken)
        {
            Trace.Add("plain stage");
            return next.InvokeAsync(cancellationToken);
        }
    }

    private sealed class TypedVoidStage : IValueRequestStage<VoidValue, NoResult>
    {
        public ValueTask<NoResult> HandleAsync(
            VoidValue request,
            ValueContinuation<NoResult> next,
            CancellationToken cancellationToken)
        {
            Trace.Add("typed stage");
            return next.InvokeAsync(cancellationToken);
        }
    }

    private sealed class FilteredStage<TRequest, TResponse> : IValueRequestStage<TRequest, TResponse>
        where TRequest : IValueRequest<TResponse>
    {
        public ValueTask<TResponse> HandleAsync(
            TRequest request,
            ValueContinuation<TResponse> next,
            CancellationToken cancellationToken)
        {
            Trace.Add("filtered stage");
            return next.InvokeAsync(cancellationToken);
        }
    }

    private sealed class TaskAndValueStage
        : IRequestStage<TaskDual, string>, IValueRequestStage<ValueDual, string>
    {
        public Task<string> HandleAsync(
            TaskDual request,
            Continuation<string> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);

        public ValueTask<string> HandleAsync(
            ValueDual request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    #endregion
}
