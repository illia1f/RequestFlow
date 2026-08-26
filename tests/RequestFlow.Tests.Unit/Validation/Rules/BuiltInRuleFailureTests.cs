using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class BuiltInRuleFailureTests
{
    [Fact]
    public void Given_A_Built_In_Rule_Throwing_When_Validating_Then_The_Exception_Surfaces_As_Thrown()
    {
        var registry = new RequestFlowRegistry();
        registry.AllowUnhandledRequests();
        registry.Add([new UnloadableInterfacesType(typeof(Probe))], []);
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();

        TypeLoadException exception =
            Should.Throw<TypeLoadException>(() => registry.Freeze(provider));

        exception.Message.ShouldBe("Could not load type 'Contracts.IAudited'.");
    }

    #region Helpers

    // Stands in for a request type; the rule under test reads its interfaces, never its contract.
    private sealed class Probe
    { }

    // A request type whose interface list lives in an assembly the application did not deploy;
    // MultiContractRequestRule reads that list for every request.
    private sealed class UnloadableInterfacesType(Type inner) : TypeDelegator(inner)
    {
        public override Type[] GetInterfaces()
            => throw new TypeLoadException("Could not load type 'Contracts.IAudited'.");
    }

    #endregion
}
