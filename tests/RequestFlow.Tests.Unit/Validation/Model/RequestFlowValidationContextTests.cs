using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class RequestFlowValidationContextTests
{
    // Reaches the internal constructor through the InternalsVisibleTo grant in
    // RequestFlow.Abstractions.csproj; an application gets a context from the freeze.
    [Fact]
    public void Given_A_Model_And_Every_Flag_When_Creating_The_Context_Then_The_Parts_Round_Trip()
    {
        RequestFlowModel model = new RequestFlowModelBuilder().AddRequest(typeof(int)).Build();

        var context = new RequestFlowValidationContext(
            model,
            allUnhandledRequestsAllowed: true,
            unusedStagesDisallowed: true,
            allUnhandledEventsAllowed: true,
            unusedEventHandlersDisallowed: true);

        context.Model.ShouldBeSameAs(model);
        context.AllUnhandledRequestsAllowed.ShouldBeTrue();
        context.UnusedStagesDisallowed.ShouldBeTrue();
        context.AllUnhandledEventsAllowed.ShouldBeTrue();
        context.UnusedEventHandlersDisallowed.ShouldBeTrue();
    }

    [Fact]
    public void Given_A_Null_Model_When_Creating_The_Context_Then_Throws_Argument_Null_Exception()
    {
        Should.Throw<ArgumentNullException>(
            () => new RequestFlowValidationContext(null!, false, false, false, false));
    }

    // Pins the binary signature a rule assembly compiled against 1.0.0-preview.7 binds to.
    [Fact]
    public void Given_The_Two_Flag_Signature_When_Resolving_It_Then_It_Is_Still_On_The_Assembly()
    {
        typeof(RequestFlowModelBuilder)
            .GetMethod(nameof(RequestFlowModelBuilder.BuildContext), [typeof(bool), typeof(bool)])
            .ShouldNotBeNull();
    }

    [Fact]
    public void Given_No_Arguments_When_Building_A_Context_Then_Both_Flags_Are_False()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder().BuildContext();

        context.AllUnhandledRequestsAllowed.ShouldBeFalse();
        context.UnusedStagesDisallowed.ShouldBeFalse();
    }

    [Fact]
    public void Given_All_Unhandled_Requests_Allowed_When_Building_A_Context_Then_Only_That_Flag_Is_Set()
    {
        RequestFlowValidationContext context =
            new RequestFlowModelBuilder().BuildContext(allUnhandledRequestsAllowed: true);

        context.AllUnhandledRequestsAllowed.ShouldBeTrue();
        context.UnusedStagesDisallowed.ShouldBeFalse();
    }

    [Fact]
    public void Given_Unused_Stages_Disallowed_When_Building_A_Context_Then_Only_That_Flag_Is_Set()
    {
        RequestFlowValidationContext context =
            new RequestFlowModelBuilder().BuildContext(unusedStagesDisallowed: true);

        context.UnusedStagesDisallowed.ShouldBeTrue();
        context.AllUnhandledRequestsAllowed.ShouldBeFalse();
    }

    [Fact]
    public void Given_A_Built_Model_When_Building_A_Context_Then_Its_Model_Has_The_Same_Shape()
    {
        RequestFlowModelBuilder builder = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), r => r.AddHandler(typeof(object), typeof(string)).AddStage(typeof(Uri), typeof(Uri)))
            .AddRequest(typeof(long))
            .AddStageDeclaration(typeof(Uri), RequestFlowLifetime.Singleton);
        RequestFlowModel expected = builder.Build();

        RequestFlowModel model = builder.BuildContext().Model;

        model.Requests.Select(r => r.RequestType).ShouldBe(expected.Requests.Select(r => r.RequestType));
        model.Requests[0].Handlers.Select(h => h.HandlerType)
            .ShouldBe(expected.Requests[0].Handlers.Select(h => h.HandlerType));
        model.Requests[0].Stages.Select(s => s.DeclaredType)
            .ShouldBe(expected.Requests[0].Stages.Select(s => s.DeclaredType));

        StageDeclarationModel declaration = model.StageDeclarations.ShouldHaveSingleItem();
        declaration.StageType.ShouldBe(typeof(Uri));
        declaration.Lifetime.ShouldBe(RequestFlowLifetime.Singleton);
        declaration.ReachedRequests.ShouldBe([typeof(int)]);
        declaration.ReachedRequests.ShouldBe(expected.StageDeclarations[0].ReachedRequests);
    }

    [Fact]
    public void Given_A_Context_Already_Built_When_Building_Another_Then_It_Wraps_Its_Own_Model()
    {
        RequestFlowModelBuilder builder = new RequestFlowModelBuilder().AddRequest(typeof(int));
        RequestFlowValidationContext first = builder.BuildContext();

        RequestFlowValidationContext second = builder.BuildContext();

        second.Model.ShouldNotBeSameAs(first.Model);
    }

    [Fact]
    public void Given_A_Hand_Built_Context_When_An_External_Rule_Validates_Then_It_Reads_Both_Flags()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .BuildContext(allUnhandledRequestsAllowed: true, unusedStagesDisallowed: true);

        RequestFlowValidationProblem problem =
            new FlagReportingRule().Validate(context).ShouldHaveSingleItem();

        problem.Message.ShouldBe("unhandled=True unused=True");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Given_Registration_Opt_Ins_When_An_External_Rule_Runs_At_Freeze_Then_It_Reads_Them_Off_The_Context(
        bool allowAllUnhandledRequests, bool disallowUnusedStages)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
            {
                o.RegisterHandlersFromAssemblyContaining<RequestFlowValidationContextTests>();
                if (allowAllUnhandledRequests)
                    o.AllowAllUnhandledRequests();
                if (disallowUnusedStages)
                    o.DisallowUnusedStages();
            })
            .AddValidationRule<FlagReportingRule>();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        exception.Problems.ShouldContain(p =>
            p.Code == FlagCode
            && p.Message == $"unhandled={allowAllUnhandledRequests} unused={disallowUnusedStages}");
    }

    [Fact]
    public void Given_Scoped_Handlers_When_An_External_Rule_Runs_At_Freeze_Then_It_Reads_The_Handler_Lifetime()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o
                .RegisterHandlersFromAssemblyContaining<RequestFlowValidationContextTests>()
                .WithScopedHandlers())
            .AddValidationRule<HandlerLifetimeReportingRule>();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        string message = exception.Problems.Single(p => p.Code == LifetimeCode).Message;
        message.ShouldContain("=Scoped");
        message.ShouldNotContain("=Transient");
    }

    // Each call decides for the handlers it found, so a rule reads the lifetime off each handler.
    [Fact]
    public void Given_Two_Calls_Choosing_Different_Handler_Lifetimes_When_A_Rule_Runs_Then_Each_Handler_Reports_Its_Own()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o
                .RegisterHandlersFromAssemblyContaining<RequestFlowValidationContextTests>()
                .WithScopedHandlers())
            .AddValidationRule<HandlerLifetimeReportingRule>();
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssembly(typeof(RequestFlow.Tests.ValidationFixtures.Lonely).Assembly)
            .AllowAllUnhandledRequests());
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        string message = exception.Problems.Single(p => p.Code == LifetimeCode).Message;
        message.ShouldContain("=Scoped");
        message.ShouldContain($"{nameof(RequestFlow.Tests.ValidationFixtures.Rooted)}=Transient");
    }

    #region Helpers

    private const string FlagCode = "TEST0200";

    private const string LifetimeCode = "TEST0201";

    private sealed class HandlerLifetimeReportingRule : IRequestFlowValidationRule
    {
        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
        {
            List<string> reported = [];
            foreach (RequestModel request in context.Model.Requests)
            {
                foreach (HandlerModel handler in request.Handlers)
                    reported.Add($"{request.RequestType.Name}={handler.Lifetime}");
            }

            return [new RequestFlowValidationProblem(LifetimeCode, string.Join(" ", reported))];
        }
    }

    // Stands in for a rule an application adds: it reports what only a built-in rule could read
    // before the context existed.
    private sealed class FlagReportingRule : IRequestFlowValidationRule
    {
        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
            => [new RequestFlowValidationProblem(
                FlagCode,
                $"unhandled={context.AllUnhandledRequestsAllowed} unused={context.UnusedStagesDisallowed}")];
    }

    #endregion
}
