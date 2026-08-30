using System.Runtime.CompilerServices;
using RequestFlow;

namespace RequestFlow.Tests.RegistrationFixtures;

public static class RegistrationBridge
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void RegisterHandlersFromCallingAssembly(RequestFlowOptions options)
        => options.RegisterHandlersFromCallingAssembly();
}
