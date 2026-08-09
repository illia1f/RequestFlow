using Microsoft.Extensions.DependencyInjection;
using RequestFlow;
using RequestFlow.Tests.ValidationFixtures;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class MultiContractRequestRuleTests
{
    [Fact]
    public void Given_A_Request_With_Two_Response_Contracts_When_Validating_Then_Reports_The_Request()
    {
        RequestFlowValidationContext context = Context(typeof(TwoContracts));

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0106");
        problem.Subject.ShouldBe(typeof(TwoContracts));
        problem.Message.ShouldContain("more than one request contract");
        problem.Message.ShouldContain("System.String");
        problem.Message.ShouldContain("System.Int32");
    }

    [Fact]
    public void Given_A_Void_Request_With_A_Typed_Contract_When_Validating_Then_Reports_The_Request()
    {
        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(Context(typeof(VoidAndTyped)))];

        problems.ShouldHaveSingleItem().Subject.ShouldBe(typeof(VoidAndTyped));
    }

    [Fact]
    public void Given_A_Request_With_One_Contract_When_Validating_Then_Reports_Nothing()
    {
        _sut.Validate(Context(typeof(SingleContract))).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Void_Request_When_Validating_Then_Reports_Nothing()
    {
        _sut.Validate(Context(typeof(VoidOnly))).ShouldBeEmpty();
    }

    [Fact]
    public void Given_Two_Marker_Interfaces_Sharing_One_Contract_When_Validating_Then_Reports_Nothing()
    {
        _sut.Validate(Context(typeof(SharedContract))).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Scanned_Multi_Contract_Request_When_Resolving_Dispatcher_Then_The_Problem_Is_In_The_Exception()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssembly(typeof(Forked).Assembly));

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IRequestDispatcher>());

        exception.Problems.ShouldContain(p => p.Code == "RF0106" && p.Subject == typeof(Forked));
    }

    #region Initialization

    private readonly MultiContractRequestRule _sut = new();

    #endregion

    #region Helpers

    private static RequestFlowValidationContext Context(Type requestType)
        => new RequestFlowModelBuilder().AddRequest(requestType).BuildContext();

    // Abstract keeps these out of the scanner when other tests scan this assembly; the rule
    // reads a type's interfaces only.
    private abstract record TwoContracts : IRequest<string>, IRequest<int>;

    private abstract record VoidAndTyped : IRequest, IRequest<int>;

    private abstract record SingleContract : IRequest<string>;

    private abstract record VoidOnly : IRequest;

    private interface IFirstMarker : IRequest<int>
    { }

    private interface ISecondMarker : IRequest<int>
    { }

    private abstract record SharedContract : IFirstMarker, ISecondMarker;

    #endregion
}
