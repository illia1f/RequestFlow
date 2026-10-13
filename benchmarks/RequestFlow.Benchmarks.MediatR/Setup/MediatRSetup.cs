using MediatR;
using MediatR.NotificationPublishers;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.MediatR;

internal static class MediatRSetup
{
    /// <summary>
    /// MediatR 12.5.0's default lifetime for <see cref="IMediator"/>, <see cref="ISender"/> and <see cref="IPublisher"/>.
    /// RequestFlow's dispatchers in this suite use it too.
    /// </summary>
    public const ServiceLifetime DispatcherLifetime = ServiceLifetime.Transient;

    private static readonly Type[] TaskStages =
    [
        typeof(MediatRTaskStage1),
        typeof(MediatRTaskStage2),
        typeof(MediatRTaskStage3),
        typeof(MediatRTaskStage4),
        typeof(MediatRTaskStage5),
    ];

    private static readonly Type[] VoidRequestStages =
    [
        typeof(MediatRVoidRequestStage1),
        typeof(MediatRVoidRequestStage2),
        typeof(MediatRVoidRequestStage3),
        typeof(MediatRVoidRequestStage4),
        typeof(MediatRVoidRequestStage5),
    ];

    private static readonly Type[] StreamStages =
    [
        typeof(MediatRStreamStage1),
        typeof(MediatRStreamStage2),
        typeof(MediatRStreamStage3),
        typeof(MediatRStreamStage4),
        typeof(MediatRStreamStage5),
    ];

    private static readonly Type[] EventHandlers =
    [
        typeof(MediatREventHandler1),
        typeof(MediatREventHandler2),
        typeof(MediatREventHandler3),
        typeof(MediatREventHandler4),
        typeof(MediatREventHandler5),
    ];

    public static ServiceCollection TaskServices(int stageCount)
        => RequestServices(stageCount, TaskStages, typeof(MediatRTaskHandler),
            typeof(IPipelineBehavior<MediatRTaskRequest, int>));

    public static ServiceCollection VoidRequestServices(int stageCount)
        => RequestServices(stageCount, VoidRequestStages, typeof(MediatRVoidRequestHandler),
            typeof(IPipelineBehavior<MediatRVoidRequest, Unit>));

    public static ServiceCollection StreamServices(int stageCount)
    {
        CheckStageCount(stageCount);

        var services = BaseServices(parallel: false, typeof(MediatRStreamHandler));

        for (int i = 0; i < stageCount; i++)
            services.AddScoped(
                typeof(IStreamPipelineBehavior<MediatRStreamRequest, int>),
                StreamStages[i]);

        return services;
    }

    public static ServiceCollection EventServices(int handlerCount, bool parallel)
    {
        if (handlerCount is not (1 or 5))
            throw new ArgumentOutOfRangeException(nameof(handlerCount));

        return BaseServices(parallel, EventHandlers.Take(handlerCount).ToArray());
    }

    public static ServiceCollection CatalogServices(int catalogSize)
    {
        Type[] tags = RuntimeCatalog.Tags(catalogSize);
        ServiceCollection services = BaseServices(parallel: false);

        foreach (Type tag in tags)
        {
            Type request = typeof(MediatRCatalogRequest<>).MakeGenericType(tag);
            Type handler = typeof(MediatRCatalogHandler<>).MakeGenericType(tag);

            services.AddScoped(
                typeof(global::MediatR.IRequestHandler<,>).MakeGenericType(request, typeof(int)),
                handler);
        }

        return services;
    }

    public static ServiceProvider Build(IServiceCollection services)
        => services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

    private static ServiceCollection RequestServices(int stageCount, Type[] stages, Type handler, Type stageContract)
    {
        CheckStageCount(stageCount);

        var services = BaseServices(parallel: false, handler);

        for (int i = 0; i < stageCount; i++)
            services.AddScoped(stageContract, stages[i]);

        return services;
    }

    private static ServiceCollection BaseServices(bool parallel, params Type[] includedTypes)
    {
        var services = new ServiceCollection();

        services.AddScoped<ScopeWitness>();
        AddMediatRServices(services, parallel, includedTypes);

        return services;
    }

    /// <summary>
    /// Adds MediatR with its default mediator lifetime and re-registers the included handlers as scoped.
    /// </summary>
    public static void AddMediatRServices(IServiceCollection services, bool parallel, params Type[] includedTypes)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssemblyContaining<MediatRTaskHandler>();
            configuration.TypeEvaluator = type => includedTypes.Contains(type);

            if (parallel)
                configuration.NotificationPublisher = new TaskWhenAllPublisher();
        });

        ServiceDescriptor[] handlers = services.Where(descriptor =>
            descriptor.ImplementationType is Type implementation && includedTypes.Contains(implementation)).ToArray();

        foreach (ServiceDescriptor descriptor in handlers)
        {
            services.Remove(descriptor);
            services.AddScoped(descriptor.ServiceType, descriptor.ImplementationType!);
        }
    }

    private static void CheckStageCount(int count)
    {
        if (count is not (0 or 1 or 5))
            throw new ArgumentOutOfRangeException(nameof(count));
    }
}
