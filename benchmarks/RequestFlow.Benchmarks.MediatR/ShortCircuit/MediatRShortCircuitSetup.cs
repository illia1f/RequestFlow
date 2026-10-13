using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures.ShortCircuit;

namespace RequestFlow.Benchmarks.MediatR;

public static class MediatRShortCircuitSetup
{
    public static ServiceProvider Build(int bufferBytes, ConstructionTrace? trace = null)
        => MediatRSetup.Build(Services(bufferBytes, trace));

    public static ServiceCollection Services(int bufferBytes, ConstructionTrace? trace = null)
    {
        var services = new ServiceCollection();

        ShortCircuitWorkload.AddDependencies(services, bufferBytes, trace);
        MediatRSetup.AddMediatRServices(services, parallel: false, typeof(MediatRShortCircuitHandler));

        services.AddScoped<IPipelineBehavior<MediatRShortCircuitRequest, int>, MediatRShortCircuitAuthorization>();
        services.AddScoped<IPipelineBehavior<MediatRShortCircuitRequest, int>, MediatRShortCircuitCache>();
        services.AddScoped<IPipelineBehavior<MediatRShortCircuitRequest, int>, MediatRShortCircuitTransaction>();

        return services;
    }

    public static void Verify()
    {
        ShortCircuitServices.VerifyRequestFlow(valueTask: false, dispatcherLifetime: MediatRSetup.DispatcherLifetime);

        ShortCircuitVerification.Verify<ISender, MediatRShortCircuitRequest>(
            "MediatR",
            (bytes, trace) => Build(bytes, trace),
            outcome => new(outcome),
            (dispatcher, message, token) => new(dispatcher.Send(message, token)),
            DependencyResolution.AllStages,
            dispatcherLifetime: MediatRSetup.DispatcherLifetime);
    }
}
