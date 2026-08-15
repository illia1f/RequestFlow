using Microsoft.Extensions.DependencyInjection;
using RequestFlow;
using RequestFlow.Tests.ValidationFixtures;

namespace RequestFlow.Tests.Unit;

public sealed class ValidateRequestFlowTests
{
    [Fact]
    public void Given_Request_Without_Handler_When_Validating_Request_Flow_Then_Throws_Validation_Exception()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssembly(typeof(Lonely).Assembly));
        using ServiceProvider provider = services.BuildServiceProvider();

        Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());
    }

    [Fact]
    public void Given_Valid_Handler_Registrations_When_Validating_Request_Flow_Then_Returns_Same_Provider()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddRequestFlowTests>());
        using ServiceProvider provider = services.BuildServiceProvider();

        IServiceProvider result = provider.ValidateRequestFlow();

        result.ShouldBeSameAs(provider);
    }

    [Fact]
    public void Given_Scope_Validation_Enabled_When_Validating_Request_Flow_Then_Does_Not_Throw()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddRequestFlowTests>());
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        Should.NotThrow(() => provider.ValidateRequestFlow());
    }
}
