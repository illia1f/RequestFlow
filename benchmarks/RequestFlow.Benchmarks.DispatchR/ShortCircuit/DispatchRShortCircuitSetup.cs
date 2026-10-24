using Microsoft.Extensions.DependencyInjection;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures.ShortCircuit;

namespace RequestFlow.Benchmarks.DispatchR;

public static class DispatchRShortCircuitSetup
{
    public static ServiceProvider Build(int bufferBytes, ConstructionTrace? trace = null)
        => DispatchRSetup.Build(Services(bufferBytes, trace));

    public static ServiceCollection Services(int bufferBytes, ConstructionTrace? trace = null)
    {
        var services = new ServiceCollection();

        ShortCircuitWorkload.AddDependencies(services, bufferBytes, trace);
        DispatchRSetup.AddShortCircuit(services);

        return services;
    }

    public static void Verify()
    {
        ShortCircuitServices.VerifyRequestFlow(valueTask: false);

        ShortCircuitVerification.Verify<global::DispatchR.IMediator, DispatchRShortCircuitRequest>(
            "DispatchR",
            (bytes, trace) => Build(bytes, trace),
            outcome => new(outcome),
            (dispatcher, message, token) => new(dispatcher.Send(message, token)),
            DependencyResolution.EntireChain,
            authorizationDisposals: 2);
    }
}
