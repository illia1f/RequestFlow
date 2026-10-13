using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures.Catalog;

namespace RequestFlow.Benchmarks.RequestFlowFixtures;

public static class RequestFlowSetup
{
    /// <summary>
    /// RequestFlow services that registration reports print besides the dispatcher.
    /// </summary>
    public static readonly Type[] ReportedServices = [typeof(IEventPublisher)];

    /// <summary>
    /// RequestFlow stage contracts whose implementations registration reports print.
    /// </summary>
    public static readonly Type[] StageContracts =
    [
        typeof(IRequestStage<,>),
        typeof(IRequestStage<>),
        typeof(IValueRequestStage<,>),
        typeof(IValueRequestStage<>),
        typeof(IStreamRequestStage<,>),
    ];

    /// <summary>
    /// RequestFlow as one side of a paired scope check.
    /// </summary>
    public static readonly ComparedLibrary Library = new("RequestFlow", Build, ReportedServices, StageContracts);

    private static readonly Type[] TaskStages =
    [
        typeof(TaskStage1),
        typeof(TaskStage2),
        typeof(TaskStage3),
        typeof(TaskStage4),
        typeof(TaskStage5),
    ];

    private static readonly Type[] ValueStages =
    [
        typeof(ValueStage1),
        typeof(ValueStage2),
        typeof(ValueStage3),
        typeof(ValueStage4),
        typeof(ValueStage5),
    ];

    private static readonly Type[] TaskVoidRequestStages =
    [
        typeof(TaskVoidRequestStage1),
        typeof(TaskVoidRequestStage2),
        typeof(TaskVoidRequestStage3),
        typeof(TaskVoidRequestStage4),
        typeof(TaskVoidRequestStage5),
    ];

    private static readonly Type[] ValueVoidRequestStages =
    [
        typeof(ValueVoidRequestStage1),
        typeof(ValueVoidRequestStage2),
        typeof(ValueVoidRequestStage3),
        typeof(ValueVoidRequestStage4),
        typeof(ValueVoidRequestStage5),
    ];

    private static readonly Type[] StreamStages =
    [
        typeof(StreamStage1),
        typeof(StreamStage2),
        typeof(StreamStage3),
        typeof(StreamStage4),
        typeof(StreamStage5),
    ];

    private static readonly MethodInfo AddHandler = typeof(RequestFlowOptions)
        .GetMethod(nameof(RequestFlowOptions.AddHandler), Type.EmptyTypes)!;

    public static ServiceCollection TaskServices(int stageCount, ServiceLifetime dispatcherLifetime = ServiceLifetime.Scoped)
        => RequestServices(
            stageCount,
            options => options.AddHandler<TaskRequestHandler>(),
            TaskStages,
            valueTask: false,
            dispatcherLifetime);

    public static ServiceCollection ValueServices(int stageCount)
        => RequestServices(
            stageCount,
            options => options.AddHandler<ValueRequestHandler>(),
            ValueStages,
            valueTask: true,
            ServiceLifetime.Scoped);

    public static ServiceCollection TaskVoidRequestServices(int stageCount, ServiceLifetime dispatcherLifetime = ServiceLifetime.Scoped)
        => RequestServices(
            stageCount,
            options => options.AddHandler<TaskVoidRequestHandler>(),
            TaskVoidRequestStages,
            valueTask: false,
            dispatcherLifetime);

    public static ServiceCollection ValueVoidRequestServices(int stageCount)
        => RequestServices(
            stageCount,
            options => options.AddHandler<ValueVoidRequestHandler>(),
            ValueVoidRequestStages,
            valueTask: true,
            ServiceLifetime.Scoped);

    public static ServiceCollection StreamServices(int stageCount, ServiceLifetime dispatcherLifetime = ServiceLifetime.Scoped)
    {
        CheckStageCount(stageCount);

        var services = CreateServices();

        services.AddRequestFlow(options =>
        {
            SetDispatcherLifetime(options, dispatcherLifetime);
            options.WithScopedHandlers().AddHandler<StreamHandler>();

            for (int i = 0; i < stageCount; i++)
                options.AddStreamStage(StreamStages[i], stage => stage.AsScoped());
        });

        return services;
    }

    public static ServiceCollection EventServices(
        int handlerCount, bool parallel = false, ServiceLifetime dispatcherLifetime = ServiceLifetime.Scoped)
    {
        if (handlerCount is not (1 or 5))
            throw new ArgumentOutOfRangeException(nameof(handlerCount));

        var services = CreateServices();

        services.AddRequestFlow(options =>
        {
            SetDispatcherLifetime(options, dispatcherLifetime);
            options.WithScopedHandlers().AddEventHandler<EventMessageHandler1>();

            if (handlerCount == 5)
            {
                options.AddEventHandler<EventMessageHandler2>();
                options.AddEventHandler<EventMessageHandler3>();
                options.AddEventHandler<EventMessageHandler4>();
                options.AddEventHandler<EventMessageHandler5>();
            }

            if (parallel)
                options.PublishEventsInParallel();
            else
                options.PublishEventsSequentially();
        });

        return services;
    }

    /// <summary>
    /// Registers one scoped <see cref="RuntimeTaskHandler{TTag}"/> per tag in <see cref="RuntimeCatalog.Tags"/>.
    /// </summary>
    public static ServiceCollection CatalogServices(int count, ServiceLifetime dispatcherLifetime = ServiceLifetime.Scoped)
    {
        Type[] tags = RuntimeCatalog.Tags(count);
        var services = CreateServices();

        services.AddRequestFlow(options =>
        {
            SetDispatcherLifetime(options, dispatcherLifetime);
            options.WithScopedHandlers();

            foreach (Type tag in tags)
                AddHandler.MakeGenericMethod(typeof(RuntimeTaskHandler<>).MakeGenericType(tag)).Invoke(options, null);
        });

        return services;
    }

    public static ServiceProvider Build(IServiceCollection services)
    {
        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        provider.ValidateRequestFlow();

        return provider;
    }

    private static ServiceCollection RequestServices(
        int stageCount,
        Action<RequestFlowOptions> registerHandler,
        Type[] stageTypes,
        bool valueTask,
        ServiceLifetime dispatcherLifetime)
    {
        CheckStageCount(stageCount);

        var services = CreateServices();

        services.AddRequestFlow(options =>
        {
            SetDispatcherLifetime(options, dispatcherLifetime);
            options.WithScopedHandlers();
            registerHandler(options);

            for (int i = 0; i < stageCount; i++)
            {
                Type stageType = stageTypes[i];

                if (valueTask)
                    options.AddValueStage(stageType, stage => stage.AsScoped());
                else
                    options.AddStage(stageType, stage => stage.AsScoped());
            }
        });

        return services;
    }

    internal static void SetDispatcherLifetime(RequestFlowOptions options, ServiceLifetime dispatcherLifetime)
    {
        if (dispatcherLifetime == ServiceLifetime.Transient)
            options.WithTransientDispatcher();
        else if (dispatcherLifetime != ServiceLifetime.Scoped)
            throw new ArgumentOutOfRangeException(nameof(dispatcherLifetime), dispatcherLifetime, "RequestFlow dispatchers are scoped or transient.");
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();

        services.AddScoped<ScopeWitness>();

        return services;
    }

    private static void CheckStageCount(int stageCount)
    {
        if (stageCount is not (0 or 1 or 5))
            throw new ArgumentOutOfRangeException(nameof(stageCount));
    }
}
