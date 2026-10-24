using DispatchR.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.DispatchR;

internal static class DispatchRSetup
{
    private static readonly Type[] TaskStages =
    [
        typeof(DispatchRTaskStage1),
        typeof(DispatchRTaskStage2),
        typeof(DispatchRTaskStage3),
        typeof(DispatchRTaskStage4),
        typeof(DispatchRTaskStage5)
    ];

    private static readonly Type[] ValueStages =
    [
        typeof(DispatchRValueStage1),
        typeof(DispatchRValueStage2),
        typeof(DispatchRValueStage3),
        typeof(DispatchRValueStage4),
        typeof(DispatchRValueStage5)
    ];

    private static readonly Type[] VoidRequestStages =
    [
        typeof(DispatchRVoidRequestStage1),
        typeof(DispatchRVoidRequestStage2),
        typeof(DispatchRVoidRequestStage3),
        typeof(DispatchRVoidRequestStage4),
        typeof(DispatchRVoidRequestStage5)
    ];

    private static readonly Type[] ValueVoidRequestStages =
    [
        typeof(DispatchRValueVoidRequestStage1),
        typeof(DispatchRValueVoidRequestStage2),
        typeof(DispatchRValueVoidRequestStage3),
        typeof(DispatchRValueVoidRequestStage4),
        typeof(DispatchRValueVoidRequestStage5)
    ];

    private static readonly Type[] StreamStages =
    [
        typeof(DispatchRStreamStage1),
        typeof(DispatchRStreamStage2),
        typeof(DispatchRStreamStage3),
        typeof(DispatchRStreamStage4),
        typeof(DispatchRStreamStage5)
    ];

    private static readonly Type[] PipelineContracts =
    [
        typeof(global::DispatchR.Abstractions.Send.IPipelineBehavior<,>),
        typeof(global::DispatchR.Abstractions.Stream.IStreamPipelineBehavior<,>)
    ];

    private static readonly Type[] EventHandlers =
    [
        typeof(DispatchREventHandler1),
        typeof(DispatchREventHandler2),
        typeof(DispatchREventHandler3),
        typeof(DispatchREventHandler4),
        typeof(DispatchREventHandler5)
    ];

    public static ServiceCollection TaskServices(int stageCount)
        => Services(stageCount, TaskStages, typeof(DispatchRTaskHandler));

    public static ServiceCollection ValueServices(int stageCount)
        => Services(stageCount, ValueStages, typeof(DispatchRValueHandler));

    public static ServiceCollection VoidRequestServices(int stageCount)
        => Services(stageCount, VoidRequestStages, typeof(DispatchRVoidRequestHandler));

    public static ServiceCollection ValueVoidRequestServices(int stageCount)
        => Services(stageCount, ValueVoidRequestStages, typeof(DispatchRValueVoidRequestHandler));

    public static ServiceCollection StreamServices(int stageCount)
    {
        CheckStageCount(stageCount);

        return BaseServices(StreamStages.Take(stageCount).ToArray(), notifications: false, typeof(DispatchRStreamHandler));
    }

    public static ServiceCollection EventServices(int handlerCount)
    {
        if (handlerCount is not (1 or 5))
            throw new ArgumentOutOfRangeException(nameof(handlerCount));

        ServiceCollection services = BaseServices([], notifications: true);

        services.RemoveAll<global::DispatchR.Abstractions.Notification.INotificationHandler<DispatchREvent>>();

        foreach (Type handler in EventHandlers.Take(handlerCount))
            services.AddScoped(
                typeof(global::DispatchR.Abstractions.Notification.INotificationHandler<DispatchREvent>),
                handler);

        return services;
    }

    public static ServiceCollection CatalogServices(int catalogSize)
    {
        Type[] tags = RuntimeCatalog.Tags(catalogSize);
        ServiceCollection services = BaseServices([], notifications: false);

        foreach (Type tag in tags)
        {
            Type request = typeof(DispatchRCatalogRequest<>).MakeGenericType(tag);
            Type handler = typeof(DispatchRCatalogHandler<>).MakeGenericType(tag);

            services.AddScoped(
                typeof(global::DispatchR.Abstractions.Send.IRequestHandler<,>).MakeGenericType(
                    request,
                    typeof(Task<int>)),
                handler);
        }

        return services;
    }

    public static void AddShortCircuit(IServiceCollection services)
        => RegisterDispatchR(
            services,
            [
                typeof(DispatchRShortCircuitAuthorization),
                typeof(DispatchRShortCircuitCache),
                typeof(DispatchRShortCircuitTransaction)
            ],
            notifications: false,
            typeof(DispatchRShortCircuitHandler));

    public static ServiceProvider Build(IServiceCollection services)
        => services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

    private static ServiceCollection Services(int stageCount, Type[] stages, Type handler)
    {
        CheckStageCount(stageCount);

        return BaseServices(stages.Take(stageCount).ToArray(), notifications: false, handler);
    }

    private static ServiceCollection BaseServices(Type[] pipelineOrder, bool notifications, params Type[] handlers)
    {
        var services = new ServiceCollection();

        services.AddScoped<ScopeWitness>();
        RegisterDispatchR(services, pipelineOrder, notifications, handlers);

        return services;
    }

    private static void RegisterDispatchR(IServiceCollection services, Type[] pipelineOrder, bool notifications, params Type[] handlers)
    {
        services.AddDispatchR(options =>
        {
            options.Assemblies.Add(typeof(DispatchRTaskHandler).Assembly);
            options.RegisterPipelines = pipelineOrder.Length != 0;
            options.RegisterNotifications = notifications;
            options.PipelineOrder = [.. pipelineOrder];
            options.ExcludeHandlers = [.. HandlerTypes().Where(type => !handlers.Contains(type))];
        });

        // ExcludeHandlers does not apply to pipelines, so DispatchR chains every pipeline of the request type in the assembly.
        ServiceDescriptor[] unselected = services.Where(descriptor =>
                descriptor.IsKeyedService &&
                descriptor.ServiceType == typeof(global::DispatchR.Abstractions.Send.IRequestHandler) &&
                !pipelineOrder.Contains(descriptor.KeyedImplementationType) &&
                !handlers.Contains(descriptor.KeyedImplementationType))
            .ToArray();

        foreach (ServiceDescriptor descriptor in unselected)
            services.Remove(descriptor);
    }

    private static IEnumerable<Type> HandlerTypes()
        => typeof(DispatchRTaskHandler).Assembly.GetTypes().Where(type =>
            type.IsClass && !type.IsAbstract && typeof(WorkloadComponent).IsAssignableFrom(type) &&
            !type.GetInterfaces().Any(contract => contract.IsGenericType && PipelineContracts.Contains(contract.GetGenericTypeDefinition())));

    private static void CheckStageCount(int count)
    {
        if (count is not (0 or 1 or 5))
            throw new ArgumentOutOfRangeException(nameof(count));
    }
}
