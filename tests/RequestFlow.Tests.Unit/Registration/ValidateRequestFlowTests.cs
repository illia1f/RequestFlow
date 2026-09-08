using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RequestFlow;
using RequestFlow.Tests.ValidationFixtures;

namespace RequestFlow.Tests.Unit;

public sealed class ValidateRequestFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_Request_Without_Handler_When_Validating_Request_Flow_Then_Reports_The_Missing_Handler(
        bool removeDispatcher)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssembly(typeof(Lonely).Assembly));
        if (removeDispatcher)
            services.RemoveAll<IRequestDispatcher>();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.ValidateRequestFlow());

        exception.Problems.ShouldContain(problem =>
            problem.Code == ProblemCodes.UnhandledRequest && problem.Subject == typeof(Lonely));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Given_Valid_Handler_Registrations_When_Validating_Request_Flow_Then_Returns_Same_Provider(
        bool validateScopes, bool removeDispatcher)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddRequestFlowTests>());
        if (removeDispatcher)
            services.RemoveAll<IRequestDispatcher>();
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: validateScopes);

        IServiceProvider result = provider.ValidateRequestFlow();

        result.ShouldBeSameAs(provider);
    }
}
