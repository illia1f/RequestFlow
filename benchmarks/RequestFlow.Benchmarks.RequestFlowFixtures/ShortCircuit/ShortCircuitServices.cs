using Microsoft.Extensions.DependencyInjection;
using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.RequestFlowFixtures.ShortCircuit;

public static class ShortCircuitServices
{
    public static ServiceProvider Build(
        int bufferBytes,
        ConstructionTrace? trace = null,
        bool valueTask = false,
        ServiceLifetime dispatcherLifetime = ServiceLifetime.Scoped)
        => RequestFlowSetup.Build(Services(bufferBytes, trace, valueTask, dispatcherLifetime));

    public static ServiceCollection Services(
        int bufferBytes,
        ConstructionTrace? trace = null,
        bool valueTask = false,
        ServiceLifetime dispatcherLifetime = ServiceLifetime.Scoped)
    {
        var services = new ServiceCollection();

        ShortCircuitWorkload.AddDependencies(services, bufferBytes, trace);

        services.AddRequestFlow(options =>
        {
            RequestFlowSetup.SetDispatcherLifetime(options, dispatcherLifetime);
            options.WithScopedHandlers();

            if (valueTask)
            {
                options.AddHandler<ShortCircuitValueHandler>();
                options.AddValueStage(typeof(ShortCircuitValueAuthorization), stage => stage.AsScoped());
                options.AddValueStage(typeof(ShortCircuitValueCache), stage => stage.AsScoped());
                options.AddValueStage(typeof(ShortCircuitValueTransaction), stage => stage.AsScoped());
            }
            else
            {
                options.AddHandler<ShortCircuitTaskHandler>();
                options.AddStage(typeof(ShortCircuitTaskAuthorization), stage => stage.AsScoped());
                options.AddStage(typeof(ShortCircuitTaskCache), stage => stage.AsScoped());
                options.AddStage(typeof(ShortCircuitTaskTransaction), stage => stage.AsScoped());
            }
        });

        return services;
    }

    public static void VerifyRequestFlow(
        bool valueTask,
        Func<int, ConstructionTrace, ServiceProvider>? build = null,
        ServiceLifetime dispatcherLifetime = ServiceLifetime.Scoped)
    {
        if (valueTask)
            ShortCircuitVerification.Verify<IValueRequestDispatcher, ShortCircuitValueRequest>(
                "RequestFlow ValueTask",
                build ?? ((bytes, trace) => Build(bytes, trace, valueTask: true, dispatcherLifetime)),
                outcome => new(outcome),
                (dispatcher, message, token) => dispatcher.SendAsync(message, token),
                DependencyResolution.EnteredOnly,
                dispatcherLifetime: dispatcherLifetime);
        else
            ShortCircuitVerification.Verify<IRequestDispatcher, ShortCircuitTaskRequest>(
                "RequestFlow Task",
                build ?? ((bytes, trace) => Build(bytes, trace, dispatcherLifetime: dispatcherLifetime)),
                outcome => new(outcome),
                (dispatcher, message, token) => new(dispatcher.SendAsync(message, token)),
                DependencyResolution.EnteredOnly,
                dispatcherLifetime: dispatcherLifetime);
    }
}
