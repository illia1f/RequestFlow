using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RequestFlow;
using System.Runtime.CompilerServices;

namespace RequestFlow.Tests.Unit;

public sealed class PipelineInspectionTests
{
    [Fact]
    public void Given_A_Task_Handler_When_Inspecting_Then_Reports_The_Declared_Handler_And_Core_Service()
    {
        ServiceCollection services = CreateServices();
        services.AddRequestFlow(options => options.AddHandler<TaskHandler>());
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        RequestPipeline pipeline = provider.InspectRequestFlow<TaskRequest>();

        pipeline.RequestType.ShouldBe(typeof(TaskRequest));
        pipeline.Family.ShouldBe(RequestPipelineFamily.Task);
        pipeline.HandlerServiceType.ShouldBe(typeof(IRequestHandler<TaskRequest, string>));
        pipeline.DeclaredHandler.HandlerType.ShouldBe(typeof(TaskHandler));
        pipeline.DeclaredHandler.ResponseType.ShouldBe(typeof(string));
        pipeline.DeclaredHandler.IsVoid.ShouldBeFalse();
        pipeline.Stages.ShouldBeEmpty();
        pipeline.ExcludedStages.ShouldBeEmpty();
    }

    [Fact]
    public async Task Given_Task_Stages_When_Inspecting_Then_Reports_Their_Execution_Order()
    {
        ServiceCollection services = CreateServices();
        services.AddRequestFlow(options => options
            .AddHandler<TaskHandler>()
            .AddStage(typeof(TaskOuter<,>), stage => stage.AsSingleton())
            .AddStage<TaskInner>(stage => stage.WhereHandlerImplements<IDeclaredHandler>().AsScoped()));
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();

        RequestPipeline pipeline = provider.InspectRequestFlow(typeof(TaskRequest));
        string result = await scope.ServiceProvider.GetRequiredService<IRequestDispatcher>()
            .SendAsync(new TaskRequest());

        result.ShouldBe("handled");
        pipeline.Stages.Select(stage => stage.DeclaredType).ShouldBe([typeof(TaskOuter<,>), typeof(TaskInner)]);
        pipeline.Stages.Select(stage => stage.ClosedType)
            .ShouldBe([typeof(TaskOuter<TaskRequest, string>), typeof(TaskInner)]);
        pipeline.Stages.Select(stage => stage.DeclaredLifetime)
            .ShouldBe([RequestFlowLifetime.Singleton, RequestFlowLifetime.Scoped]);
        pipeline.Stages[0].HandlerFilter.ShouldBeNull();
        pipeline.Stages[1].HandlerFilter.ShouldBe(typeof(IDeclaredHandler));
        provider.GetRequiredService<ExecutionTrace>().Entries.ShouldBe(
            pipeline.Stages.Select(stage => stage.ClosedType).Concat([typeof(TaskHandler)]));
    }

    [Fact]
    public async Task Given_Value_Task_Stages_When_Inspecting_Then_Reports_Their_Execution_Order()
    {
        ServiceCollection services = CreateServices();
        services.AddRequestFlow(options => options
            .AddHandler<ValueHandler>()
            .AddValueStage(typeof(ValueOuter<,>))
            .AddValueStage<ValueInner>());
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestPipeline pipeline = provider.InspectRequestFlow<ValueRequest>();
        string result = await provider.GetRequiredService<IValueRequestDispatcher>().SendAsync(new ValueRequest());

        result.ShouldBe("handled");
        pipeline.Family.ShouldBe(RequestPipelineFamily.ValueTask);
        pipeline.HandlerServiceType.ShouldBe(typeof(IValueRequestHandler<ValueRequest, string>));
        pipeline.DeclaredHandler.ContractType.ShouldBe(typeof(IValueRequestHandler<,>));
        pipeline.Stages.Select(stage => stage.ClosedType)
            .ShouldBe([typeof(ValueOuter<ValueRequest, string>), typeof(ValueInner)]);
        provider.GetRequiredService<ExecutionTrace>().Entries.ShouldBe(
            pipeline.Stages.Select(stage => stage.ClosedType).Concat([typeof(ValueHandler)]));
    }

    [Fact]
    public async Task Given_Stream_Stages_When_Inspecting_Then_Reports_Their_Enumeration_Order()
    {
        ServiceCollection services = CreateServices();
        services.AddRequestFlow(options => options
            .AddHandler<StreamHandler>()
            .AddStreamStage(typeof(StreamOuter<,>))
            .AddStreamStage<StreamInner>());
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestPipeline pipeline = provider.InspectRequestFlow<StreamRequest>();
        provider.GetRequiredService<ExecutionTrace>().Entries.ShouldBeEmpty();
        List<int> items = [];
        await foreach (int item in provider.GetRequiredService<IStreamDispatcher>().Stream(new StreamRequest()))
            items.Add(item);

        items.ShouldBe([1]);
        pipeline.Family.ShouldBe(RequestPipelineFamily.Stream);
        pipeline.HandlerServiceType.ShouldBe(typeof(IStreamRequestHandler<StreamRequest, int>));
        pipeline.DeclaredHandler.ResponseType.ShouldBe(typeof(int));
        pipeline.DeclaredHandler.IsVoid.ShouldBeFalse();
        pipeline.Stages.Select(stage => stage.ClosedType)
            .ShouldBe([typeof(StreamOuter<StreamRequest, int>), typeof(StreamInner)]);
        provider.GetRequiredService<ExecutionTrace>().Entries.ShouldBe(
            pipeline.Stages.Select(stage => stage.ClosedType).Concat([typeof(StreamHandler)]));
    }

    [Theory]
    [InlineData(typeof(TaskVoidRequest), typeof(IRequestHandler<TaskVoidRequest>), RequestPipelineFamily.Task)]
    [InlineData(typeof(ValueVoidRequest), typeof(IValueRequestHandler<ValueVoidRequest>), RequestPipelineFamily.ValueTask)]
    public void Given_A_Void_Handler_When_Inspecting_Then_Reports_The_Void_Contract_Without_A_Response_Type(
        Type requestType, Type handlerServiceType, RequestPipelineFamily family)
    {
        ServiceCollection services = CreateServices();
        services.AddRequestFlow(options => options
            .AddHandler<TaskVoidHandler>()
            .AddHandler<ValueVoidHandler>()
            .AddStage<TaskVoidStage>()
            .AddValueStage<ValueVoidStage>());
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestPipeline pipeline = provider.InspectRequestFlow(requestType);

        pipeline.Family.ShouldBe(family);
        pipeline.HandlerServiceType.ShouldBe(handlerServiceType);
        pipeline.DeclaredHandler.ResponseType.ShouldBeNull();
        pipeline.DeclaredHandler.IsVoid.ShouldBeTrue();
        pipeline.DeclaredHandler.ContractType.ShouldBe(handlerServiceType.GetGenericTypeDefinition());
        pipeline.Stages.Count.ShouldBe(1);
        pipeline.ExcludedStages.Single().ReasonCode.ShouldBe(StageExclusionReason.DifferentFamily);
    }

    [Fact]
    public void Given_Excluded_Stages_When_Inspecting_Then_Reports_The_First_Failed_Check_In_Declaration_Order()
    {
        ServiceCollection services = CreateServices();
        services.AddRequestFlow(options => options
            .AddHandler<TaskHandler>()
            .AddValueStage(typeof(ValueOuter<,>), stage => stage.WhereHandlerImplements<IReplacementHandler>())
            .AddStage(typeof(FilteredConstrainedStage<,>), stage => stage.WhereHandlerImplements<IReplacementHandler>())
            .AddStage(typeof(ConstrainedStage<,>))
            .AddStage<OtherTaskStage>());
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestPipeline pipeline = provider.InspectRequestFlow<TaskRequest>();

        pipeline.Stages.ShouldBeEmpty();
        pipeline.ExcludedStages.Select(stage => stage.DeclaredType).ShouldBe(
        [
            typeof(ValueOuter<,>), typeof(FilteredConstrainedStage<,>), typeof(ConstrainedStage<,>),
            typeof(OtherTaskStage)
        ]);
        pipeline.ExcludedStages.Select(stage => stage.ReasonCode).ShouldBe(
        [
            StageExclusionReason.DifferentFamily,
            StageExclusionReason.HandlerFilterNotMatched,
            StageExclusionReason.GenericConstraintsNotSatisfied,
            StageExclusionReason.ContractNotCompatible
        ]);
        pipeline.ExcludedStages[0].HandlerFilter.ShouldBe(typeof(IReplacementHandler));
        pipeline.ExcludedStages[1].HandlerFilter.ShouldBe(typeof(IReplacementHandler));
        pipeline.ExcludedStages[2].HandlerFilter.ShouldBeNull();
    }

    [Fact]
    public async Task Given_A_Contravariant_Closed_Stage_When_Inspecting_Then_Includes_The_Base_Request_Stage()
    {
        ServiceCollection services = CreateServices();
        services.AddRequestFlow(options => options.AddHandler<TaskHandler>().AddStage<BaseTaskStage>());
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestPipeline pipeline = provider.InspectRequestFlow<TaskRequest>();
        await provider.GetRequiredService<IRequestDispatcher>().SendAsync(new TaskRequest());

        pipeline.Stages.Single().DeclaredType.ShouldBe(typeof(BaseTaskStage));
        pipeline.Stages.Single().ClosedType.ShouldBe(typeof(BaseTaskStage));
        pipeline.ExcludedStages.ShouldBeEmpty();
        provider.GetRequiredService<ExecutionTrace>().Entries.ShouldBe([typeof(BaseTaskStage), typeof(TaskHandler)]);
    }

    [Fact]
    public async Task Given_Additive_Registrations_When_Inspecting_Then_Preserves_Stages_Across_Calls()
    {
        ServiceCollection services = CreateServices();
        services.AddRequestFlow(options => options.AddStage(typeof(TaskOuter<,>)));
        services.AddRequestFlow(options => options.AddHandler<TaskHandler>().AddStage<TaskInner>());
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestPipeline pipeline = provider.InspectRequestFlow<TaskRequest>();
        await provider.GetRequiredService<IRequestDispatcher>().SendAsync(new TaskRequest());

        pipeline.Stages.Select(stage => stage.ClosedType)
            .ShouldBe([typeof(TaskOuter<TaskRequest, string>), typeof(TaskInner)]);
        provider.GetRequiredService<ExecutionTrace>().Entries.ShouldBe(
            pipeline.Stages.Select(stage => stage.ClosedType).Concat([typeof(TaskHandler)]));
    }

    [Fact]
    public void Given_Cached_Metadata_When_Trying_To_Mutate_Stage_Lists_Then_The_Lists_Stay_Unchanged()
    {
        ServiceCollection services = CreateServices();
        services.AddRequestFlow(options => options
            .AddHandler<TaskHandler>().AddStage<TaskInner>().AddValueStage<ValueInner>());
        using ServiceProvider provider = services.BuildServiceProvider();
        RequestPipeline pipeline = provider.InspectRequestFlow<TaskRequest>();

        CheckReadOnly(pipeline.Stages);
        CheckReadOnly(pipeline.ExcludedStages);

        RequestPipeline repeated = provider.InspectRequestFlow(typeof(TaskRequest));
        repeated.ShouldBeSameAs(pipeline);
        repeated.Stages.Single().ClosedType.ShouldBe(typeof(TaskInner));
        repeated.ExcludedStages.Single().DeclaredType.ShouldBe(typeof(ValueInner));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_A_Null_Provider_When_Inspecting_Then_Throws_Argument_Null_Exception(bool generic)
    {
        IServiceProvider provider = null!;

        Should.Throw<ArgumentNullException>(() => generic
            ? provider.InspectRequestFlow<TaskRequest>()
            : provider.InspectRequestFlow(typeof(TaskRequest)));
    }

    [Fact]
    public void Given_A_Null_Request_Type_When_Inspecting_Then_Throws_Argument_Null_Exception()
    {
        using ServiceProvider provider = CreateServices().BuildServiceProvider();

        Should.Throw<ArgumentNullException>(() => provider.InspectRequestFlow(null!));
    }

    [Theory]
    [InlineData(typeof(UnknownRequest<int>))]
    [InlineData(typeof(string))]
    public void Given_An_Unknown_Request_Type_When_Inspecting_Then_Throws_Handler_Not_Found(Type requestType)
    {
        ServiceCollection services = CreateServices();
        services.AddRequestFlow(options => options.AddHandler<TaskHandler>());
        using ServiceProvider provider = services.BuildServiceProvider();

        Should.Throw<HandlerNotFoundException>(() => provider.InspectRequestFlow(requestType));
    }

    [Fact]
    public void Given_An_Allowed_Unhandled_Request_When_Inspecting_Then_Throws_Handler_Not_Found()
    {
        ServiceCollection services = CreateServices();
        services.AddRequestFlow(options => options
            .RegisterHandlersFromAssemblyContaining<ManualHandlerRegistrationTests>()
            .ExcludeHandler<ManualHandlerRegistrationTests.SwappablePingHandler>()
            .AllowAllUnhandledRequests());
        using ServiceProvider provider = services.BuildServiceProvider();
        provider.ValidateRequestFlow();

        Should.Throw<HandlerNotFoundException>(() =>
            provider.InspectRequestFlow<ManualHandlerRegistrationTests.SwappablePing>());
    }

    [Fact]
    public void Given_An_Unrelated_Invalid_Registration_When_Inspecting_Then_Throws_The_Validation_Exception()
    {
        ServiceCollection services = CreateServices();
        services.AddRequestFlow(options => options
            .AddHandler<TaskHandler>()
            .AddHandler<ValueHandler>()
            .AddHandler<DuplicateValueHandler<int>>());
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            provider.InspectRequestFlow<TaskRequest>());

        exception.Problems.ShouldContain(problem => problem.Code == ProblemCodes.DuplicateHandler);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_Inspection_And_Dispatch_When_Resolving_In_Either_Order_Then_They_Share_One_Validation(
        bool inspectFirst)
    {
        ServiceCollection services = CreateServices();
        var rule = new CountingRule();
        services.AddSingleton<IRequestFlowValidationRule>(rule);
        services.AddRequestFlow(options => options.AddHandler<TaskHandler>().WithScopedHandlers());
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();

        RequestPipeline? first = inspectFirst ? provider.InspectRequestFlow<TaskRequest>() : null;
        await scope.ServiceProvider.GetRequiredService<IRequestDispatcher>().SendAsync(new TaskRequest());
        RequestPipeline pipeline = scope.ServiceProvider.InspectRequestFlow<TaskRequest>();
        provider.ValidateRequestFlow();
        provider.InspectRequestFlow(typeof(TaskRequest)).ShouldBeSameAs(pipeline);

        rule.Calls.ShouldBe(1);
        pipeline.DeclaredHandler.ShouldBeSameAs(rule.Context!.Model.Requests
            .Single(request => request.RequestType == typeof(TaskRequest)).Handlers.Single());
        if (inspectFirst)
            pipeline.ShouldBeSameAs(first);
    }

    [Fact]
    public void Given_Two_Providers_When_Inspecting_Then_Each_Provider_Freezes_Its_Own_Metadata()
    {
        ServiceCollection services = CreateServices();
        var rule = new CountingRule();
        services.AddSingleton<IRequestFlowValidationRule>(rule);
        services.AddRequestFlow(options => options.AddHandler<TaskHandler>());
        using ServiceProvider first = services.BuildServiceProvider();
        using ServiceProvider second = services.BuildServiceProvider();

        RequestPipeline firstPipeline = first.InspectRequestFlow<TaskRequest>();
        RequestPipeline secondPipeline = second.InspectRequestFlow<TaskRequest>();

        firstPipeline.ShouldNotBeSameAs(secondPipeline);
        rule.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task Given_Handler_And_Stage_Factories_When_Inspecting_Then_Only_Dispatch_Invokes_The_Factories()
    {
        ServiceCollection services = CreateServices();
        services.AddRequestFlow(options => options
            .AddHandler<TaskHandler>().AddStage<TaskInner>()
            .AddHandler<ValueHandler>().AddValueStage<ValueInner>()
            .AddHandler<StreamHandler>().AddStreamStage<StreamInner>());
        int factoryCalls = 0;
        services.Replace(ServiceDescriptor.Transient<IRequestHandler<TaskRequest, string>>(provider =>
        {
            factoryCalls++;
            return new TaskHandler(provider.GetRequiredService<ExecutionTrace>());
        }));
        services.Replace(ServiceDescriptor.Transient<IValueRequestHandler<ValueRequest, string>>(provider =>
        {
            factoryCalls++;
            return new ValueHandler(provider.GetRequiredService<ExecutionTrace>());
        }));
        services.Replace(ServiceDescriptor.Transient<IStreamRequestHandler<StreamRequest, int>>(provider =>
        {
            factoryCalls++;
            return new StreamHandler(provider.GetRequiredService<ExecutionTrace>());
        }));
        services.Replace(ServiceDescriptor.Transient<TaskInner>(provider =>
        {
            factoryCalls++;
            return new TaskInner(provider.GetRequiredService<ExecutionTrace>());
        }));
        services.Replace(ServiceDescriptor.Transient<ValueInner>(provider =>
        {
            factoryCalls++;
            return new ValueInner(provider.GetRequiredService<ExecutionTrace>());
        }));
        services.Replace(ServiceDescriptor.Transient<StreamInner>(provider =>
        {
            factoryCalls++;
            return new StreamInner(provider.GetRequiredService<ExecutionTrace>());
        }));
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.InspectRequestFlow<TaskRequest>();
        provider.InspectRequestFlow<ValueRequest>();
        provider.InspectRequestFlow<StreamRequest>();

        factoryCalls.ShouldBe(0);
        provider.GetRequiredService<ExecutionTrace>().Entries.ShouldBeEmpty();
        await provider.GetRequiredService<IRequestDispatcher>().SendAsync(new TaskRequest());
        await provider.GetRequiredService<IValueRequestDispatcher>().SendAsync(new ValueRequest());
        await foreach (int _ in provider.GetRequiredService<IStreamDispatcher>().Stream(new StreamRequest()))
        { }
        factoryCalls.ShouldBe(6);
    }

    [Fact]
    public async Task Given_DI_Replacements_When_Inspecting_Then_Selection_And_Lifetimes_Describe_The_Declared_Handler()
    {
        ServiceCollection services = CreateServices();
        services.AddRequestFlow(options => options
            .AddHandler<TaskHandler>().WithScopedHandlers()
            .AddStage<TaskInner>(stage => stage.WhereHandlerImplements<IDeclaredHandler>().AsScoped())
            .AddStage(typeof(TaskOuter<,>), stage => stage.WhereHandlerImplements<IReplacementHandler>()));
        services.Replace(ServiceDescriptor.Singleton<IRequestHandler<TaskRequest, string>, ReplacementTaskHandler<int>>());
        services.Replace(ServiceDescriptor.Singleton<TaskInner, TaskInner>());
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();

        RequestPipeline pipeline = provider.InspectRequestFlow<TaskRequest>();
        string result = await scope.ServiceProvider.GetRequiredService<IRequestDispatcher>().SendAsync(new TaskRequest());

        result.ShouldBe("replacement");
        pipeline.HandlerServiceType.ShouldBe(typeof(IRequestHandler<TaskRequest, string>));
        pipeline.DeclaredHandler.HandlerType.ShouldBe(typeof(TaskHandler));
        pipeline.DeclaredHandler.Lifetime.ShouldBe(RequestFlowLifetime.Scoped);
        pipeline.Stages.Single().DeclaredLifetime.ShouldBe(RequestFlowLifetime.Scoped);
        pipeline.Stages.Single().HandlerFilter.ShouldBe(typeof(IDeclaredHandler));
        pipeline.ExcludedStages.Single().ReasonCode.ShouldBe(StageExclusionReason.HandlerFilterNotMatched);
        provider.GetRequiredService<ExecutionTrace>().Entries.ShouldBe([typeof(TaskInner), typeof(ReplacementTaskHandler<int>)]);
    }

    #region Helpers

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ExecutionTrace>();
        return services;
    }

    private static void CheckReadOnly<T>(IReadOnlyList<T> values)
    {
        values.ShouldNotBeOfType<T[]>();
        if (values is ICollection<T> collection)
        {
            collection.IsReadOnly.ShouldBeTrue();
            Should.Throw<NotSupportedException>(() => collection.Clear());
        }

        if (values is IList<T> list)
            Should.Throw<NotSupportedException>(() => list[0] = list[0]);
    }

    private sealed class ExecutionTrace
    {
        public List<Type> Entries { get; } = [];
    }

    private interface IDeclaredHandler
    { }

    private interface IReplacementHandler
    { }

    private interface ITaggedRequest
    { }

    private abstract record BaseRequest : IRequest<string>;

    private sealed record TaskRequest : BaseRequest;

    private sealed record UnknownRequest<T> : IRequest<string>;

    private sealed record ValueRequest : IValueRequest<string>;

    private sealed record StreamRequest : IStreamRequest<int>;

    private sealed record TaskVoidRequest : IRequest;

    private sealed record ValueVoidRequest : IValueRequest;

    private sealed class CountingRule : IRequestFlowValidationRule
    {
        public int Calls { get; private set; }

        public RequestFlowValidationContext? Context { get; private set; }

        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
        {
            Calls++;
            Context = context;
            return [];
        }
    }

    private sealed class TaskHandler(ExecutionTrace trace) : IRequestHandler<TaskRequest, string>, IDeclaredHandler
    {
        public Task<string> HandleAsync(TaskRequest request, CancellationToken cancellationToken)
        {
            trace.Entries.Add(typeof(TaskHandler));
            return Task.FromResult("handled");
        }
    }

    private sealed class ReplacementTaskHandler<T>(ExecutionTrace trace) :
        IRequestHandler<TaskRequest, string>, IReplacementHandler
    {
        public Task<string> HandleAsync(TaskRequest request, CancellationToken cancellationToken)
        {
            trace.Entries.Add(typeof(ReplacementTaskHandler<T>));
            return Task.FromResult("replacement");
        }
    }

    private sealed class ValueHandler(ExecutionTrace trace) : IValueRequestHandler<ValueRequest, string>
    {
        public ValueTask<string> HandleAsync(ValueRequest request, CancellationToken cancellationToken)
        {
            trace.Entries.Add(typeof(ValueHandler));
            return new ValueTask<string>("handled");
        }
    }

    private sealed class DuplicateValueHandler<T> : IValueRequestHandler<ValueRequest, string>
    {
        public ValueTask<string> HandleAsync(ValueRequest request, CancellationToken cancellationToken)
            => new("duplicate");
    }

    private sealed class StreamHandler(ExecutionTrace trace) : IStreamRequestHandler<StreamRequest, int>
    {
        public async IAsyncEnumerable<int> Handle(
            StreamRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            trace.Entries.Add(typeof(StreamHandler));
            await Task.CompletedTask;
            yield return 1;
        }
    }

    private sealed class TaskVoidHandler : IRequestHandler<TaskVoidRequest>
    {
        public Task HandleAsync(TaskVoidRequest request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class ValueVoidHandler : IValueRequestHandler<ValueVoidRequest>
    {
        public ValueTask HandleAsync(ValueVoidRequest request, CancellationToken cancellationToken)
            => default;
    }

    private sealed class TaskOuter<TRequest, TResponse>(ExecutionTrace trace) : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
        {
            trace.Entries.Add(typeof(TaskOuter<TRequest, TResponse>));
            return next.InvokeAsync(cancellationToken);
        }
    }

    private sealed class TaskInner(ExecutionTrace trace) : IRequestStage<TaskRequest, string>
    {
        public Task<string> HandleAsync(TaskRequest request, Continuation<string> next, CancellationToken cancellationToken)
        {
            trace.Entries.Add(typeof(TaskInner));
            return next.InvokeAsync(cancellationToken);
        }
    }

    private sealed class ValueOuter<TRequest, TResponse>(ExecutionTrace trace) : IValueRequestStage<TRequest, TResponse>
        where TRequest : IValueRequest<TResponse>
    {
        public ValueTask<TResponse> HandleAsync(
            TRequest request, ValueContinuation<TResponse> next, CancellationToken cancellationToken)
        {
            trace.Entries.Add(typeof(ValueOuter<TRequest, TResponse>));
            return next.InvokeAsync(cancellationToken);
        }
    }

    private sealed class ValueInner(ExecutionTrace trace) : IValueRequestStage<ValueRequest, string>
    {
        public ValueTask<string> HandleAsync(ValueRequest request, ValueContinuation<string> next, CancellationToken cancellationToken)
        {
            trace.Entries.Add(typeof(ValueInner));
            return next.InvokeAsync(cancellationToken);
        }
    }

    private sealed class StreamOuter<TRequest, TItem>(ExecutionTrace trace) : IStreamRequestStage<TRequest, TItem>
        where TRequest : IStreamRequest<TItem>
    {
        public async IAsyncEnumerable<TItem> Handle(
            TRequest request, StreamContinuation<TItem> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            trace.Entries.Add(typeof(StreamOuter<TRequest, TItem>));
            await foreach (TItem item in next.Invoke(cancellationToken))
                yield return item;
        }
    }

    private sealed class StreamInner(ExecutionTrace trace) : IStreamRequestStage<StreamRequest, int>
    {
        public async IAsyncEnumerable<int> Handle(
            StreamRequest request, StreamContinuation<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            trace.Entries.Add(typeof(StreamInner));
            await foreach (int item in next.Invoke(cancellationToken))
                yield return item;
        }
    }

    private sealed class TaskVoidStage : IRequestStage<TaskVoidRequest>
    {
        public Task HandleAsync(TaskVoidRequest request, Continuation next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed class ValueVoidStage : IValueRequestStage<ValueVoidRequest>
    {
        public ValueTask HandleAsync(ValueVoidRequest request, ValueContinuation next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed class FilteredConstrainedStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>, ITaggedRequest
    {
        public Task<TResponse> HandleAsync(TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed class ConstrainedStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>, ITaggedRequest
    {
        public Task<TResponse> HandleAsync(TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed class OtherTaskStage : IRequestStage<UnknownRequest<int>, string>
    {
        public Task<string> HandleAsync(UnknownRequest<int> request, Continuation<string> next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed class BaseTaskStage(ExecutionTrace trace) : IRequestStage<BaseRequest, string>
    {
        public Task<string> HandleAsync(BaseRequest request, Continuation<string> next, CancellationToken cancellationToken)
        {
            trace.Entries.Add(typeof(BaseTaskStage));
            return next.InvokeAsync(cancellationToken);
        }
    }

    #endregion
}
