using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class AddValidationRuleTests
{
    [Fact]
    public void Given_A_Failing_External_Rule_When_Validating_Then_Its_Problem_Is_In_The_Exception()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddValidationRuleTests>())
            .AddValidationRule<AlwaysFailsRule>();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        exception.Problems.ShouldContain(p => p.Code == "TEST0001");
    }

    [Fact]
    public void Given_A_Passing_External_Rule_When_Validating_Then_Does_Not_Throw()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddValidationRuleTests>())
            .AddValidationRule<NeverFailsRule>();
        using ServiceProvider provider = services.BuildServiceProvider();

        Should.NotThrow(() => provider.ValidateRequestFlow());
    }

    [Fact]
    public void Given_A_Rule_With_A_Dependency_When_Validating_Then_The_Rule_Is_Constructor_Injected()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ProblemSource("TEST0002"));
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddValidationRuleTests>())
            .AddValidationRule<DependentRule>();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        exception.Problems.ShouldContain(p => p.Code == "TEST0002");
    }

    [Fact]
    public void Given_Shape_Built_In_And_External_Problems_When_Validating_Then_Problems_Are_Ordered_Shape_Then_Built_In_Then_External()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
            {
                o.RegisterHandlersFromAssemblyContaining<AddValidationRuleTests>();
                o.RegisterHandlersFromAssembly(typeof(RequestFlow.Tests.ValidationFixtures.Lonely).Assembly);
                o.RegisterGenericHandler(typeof(string), typeof(object));
            })
            .AddValidationRule<AlwaysFailsRule>();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        int shape = IndexOfCode(exception, "RF0001");
        int builtIn = IndexOfCode(exception, "RF0102");
        int external = IndexOfCode(exception, "TEST0001");
        shape.ShouldBeGreaterThanOrEqualTo(0);
        builtIn.ShouldBeGreaterThan(shape);
        external.ShouldBeGreaterThan(builtIn);
    }

    [Fact]
    public void Given_The_Same_Rule_Added_Twice_When_Validating_Then_It_Runs_Once()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddValidationRuleTests>())
            .AddValidationRule<AlwaysFailsRule>()
            .AddValidationRule<AlwaysFailsRule>();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        exception.Problems.Count(p => p.Code == "TEST0001").ShouldBe(1);
    }

    [Fact]
    public void Given_A_Throwing_Rule_When_Validating_Then_The_Failure_Is_Reported_As_A_Problem()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddValidationRuleTests>())
            .AddValidationRule<ThrowingRule>();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        exception.Problems.ShouldContain(p => p.Code == "RF0107" && p.Subject == typeof(ThrowingRule));
    }

    [Fact]
    public void Given_A_Throwing_Rule_When_Validating_Then_The_Problem_Names_The_Rule_And_The_Exception()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddValidationRuleTests>())
            .AddValidationRule<ThrowingRule>();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        string message = exception.Problems.Single(p => p.Code == "RF0107").Message;
        message.ShouldContain(typeof(ThrowingRule).FullName!);
        message.ShouldContain(typeof(FormatException).FullName!);
        message.ShouldContain("rule blew up");
    }

    [Fact]
    public void Given_A_Rule_Throwing_From_A_Helper_When_Validating_Then_The_Original_Exception_Is_Preserved()
    {
        var rule = new HelperThrowingRule();
        var services = new ServiceCollection();
        services.AddRequestFlow(_ => { });
        services.AddSingleton<IRequestFlowValidationRule>(rule);
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        AggregateException failures = exception.InnerException.ShouldBeOfType<AggregateException>();
        failures.InnerExceptions.ShouldHaveSingleItem().ShouldBeSameAs(rule.Failure);
        exception.ToString().ShouldContain(nameof(HelperThrowingRule.ThrowFromHelper));
        exception.ToString().ShouldContain("helper blew up");
    }

    [Fact]
    public void Given_Two_Throwing_Rules_When_Validating_Then_Both_Original_Exceptions_Are_Preserved_In_Order()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(_ => { })
            .AddValidationRule<ThrowingRule>()
            .AddValidationRule<PartiallyThrowingRule>();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        AggregateException failures = exception.InnerException.ShouldBeOfType<AggregateException>();
        failures.InnerExceptions.Count.ShouldBe(2);
        failures.InnerExceptions[0].ShouldBeOfType<FormatException>().Message.ShouldBe("rule blew up");
        failures.InnerExceptions[1].ShouldBeOfType<NotSupportedException>().Message.ShouldBe("enumeration blew up");
        failures.InnerExceptions[1].StackTrace.ShouldNotBeNullOrEmpty();
        exception.Problems.ShouldNotContain(p => p.Code == "TEST0004");
    }

    [Fact]
    public void Given_A_Throwing_Rule_Beside_A_Built_In_Failure_When_Validating_Then_Both_Are_Reported()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
            {
                o.RegisterHandlersFromAssemblyContaining<AddValidationRuleTests>();
                o.RegisterHandlersFromAssembly(typeof(RequestFlow.Tests.ValidationFixtures.Lonely).Assembly);
            })
            .AddValidationRule<ThrowingRule>();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        exception.Problems.ShouldContain(p => p.Code == "RF0102");
        exception.Problems.ShouldContain(p => p.Code == "RF0107");
    }

    [Fact]
    public void Given_A_Throwing_Rule_Registered_First_When_Validating_Then_The_Later_Rule_Still_Reports()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddValidationRuleTests>())
            .AddValidationRule<ThrowingRule>()
            .AddValidationRule<AlwaysFailsRule>();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        int failure = IndexOfCode(exception, "RF0107");
        failure.ShouldBeGreaterThanOrEqualTo(0);
        IndexOfCode(exception, "TEST0001").ShouldBeGreaterThan(failure);
    }

    [Fact]
    public void Given_A_Rule_Throwing_While_Its_Sequence_Is_Enumerated_When_Validating_Then_The_Failure_Is_Reported_As_A_Problem()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddValidationRuleTests>())
            .AddValidationRule<PartiallyThrowingRule>();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        exception.Problems.ShouldContain(p => p.Code == "RF0107" && p.Subject == typeof(PartiallyThrowingRule));
    }

    [Fact]
    public void Given_A_Rule_Throwing_After_It_Yielded_A_Problem_When_Validating_Then_That_Problem_Is_Dropped()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddValidationRuleTests>())
            .AddValidationRule<PartiallyThrowingRule>();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        exception.Problems.ShouldNotContain(p => p.Code == "TEST0004");
    }

    // Only the marker type tells RequestFlow's own diagnostics apart from a rule's exception.
    [Fact]
    public void Given_A_Rule_Throwing_A_Lookalike_Diagnostic_When_Validating_Then_The_Failure_Is_Reported_As_A_Problem()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddValidationRuleTests>())
            .AddValidationRule<LookalikeThrowingRule>();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        exception.Problems.ShouldContain(p => p.Code == "RF0107" && p.Subject == typeof(LookalikeThrowingRule));
    }

    [Fact]
    public void Given_A_Null_Returning_Rule_When_Validating_Then_The_Diagnostic_Is_Not_A_Validation_Exception()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddValidationRuleTests>())
            .AddValidationRule<NullReturningRule>();
        using ServiceProvider provider = services.BuildServiceProvider();

        InvalidOperationException exception =
            Should.Throw<InvalidOperationException>(() => provider.ValidateRequestFlow());

        exception.ShouldBeOfType<InvalidOperationException>();
        exception.Message.ShouldBe(
            $"Validation rule '{typeof(NullReturningRule).FullName}' returned null instead of an empty sequence.");
    }

    [Fact]
    public void Given_A_Rule_Returning_A_Null_Problem_When_Validating_Then_The_Diagnostic_Is_Not_A_Validation_Exception()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddValidationRuleTests>())
            .AddValidationRule<NullProblemReturningRule>();
        using ServiceProvider provider = services.BuildServiceProvider();

        InvalidOperationException exception =
            Should.Throw<InvalidOperationException>(() => provider.ValidateRequestFlow());

        exception.ShouldBeOfType<InvalidOperationException>();
        exception.Message.ShouldBe(
            $"Validation rule '{typeof(NullProblemReturningRule).FullName}' returned a null problem.");
    }

    [Fact]
    public void Given_Event_Registration_When_An_External_Rule_Validates_Then_It_Reads_Event_Facts_And_Flags()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options => options
                .RegisterHandlersFromAssemblyContaining<AddValidationRuleTests>()
                .AllowAllUnhandledEvents()
                .DisallowUnusedEventHandlers())
            .AddValidationRule<EventFactsRule>();
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception =
            Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());

        RequestFlowValidationProblem problem = exception.Problems.Single(candidate =>
            candidate.Code == "TEST0005");
        problem.Subject.ShouldBe(typeof(RequestFlow.Tests.ValidationFixtures.ExternalContractEvent));
        problem.Message.ShouldBe(
            "event=True subscription=True unhandled=True unused=True");
    }

    #region Helpers

    private static int IndexOfCode(RequestFlowValidationException exception, string code)
    {
        for (int i = 0; i < exception.Problems.Count; i++)
        {
            if (exception.Problems[i].Code == code)
                return i;
        }

        return -1;
    }

    public sealed record Echo : IRequest<string>;

    public sealed class EchoHandler : IRequestHandler<Echo, string>
    {
        public Task<string> HandleAsync(Echo request, CancellationToken cancellationToken)
            => Task.FromResult(string.Empty);
    }

    public sealed class ExternalContractEventHandler :
        IEventHandler<RequestFlow.Tests.ValidationFixtures.ExternalContractEvent>
    {
        public Task HandleAsync(
            RequestFlow.Tests.ValidationFixtures.ExternalContractEvent @event,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class AlwaysFailsRule : IRequestFlowValidationRule
    {
        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
            => [new RequestFlowValidationProblem("TEST0001", "always fails")];
    }

    private sealed class NeverFailsRule : IRequestFlowValidationRule
    {
        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
            => [];
    }

    public sealed class ProblemSource(string code)
    {
        public string Code { get; } = code;
    }

    private sealed class DependentRule(ProblemSource source) : IRequestFlowValidationRule
    {
        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
            => [new RequestFlowValidationProblem(source.Code, "from dependency")];
    }

    private sealed class HelperThrowingRule : IRequestFlowValidationRule
    {
        public FormatException Failure { get; } = new("helper blew up");

        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
            => ThrowFromHelper();

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public IEnumerable<RequestFlowValidationProblem> ThrowFromHelper()
            => throw Failure;
    }

    private sealed class ThrowingRule : IRequestFlowValidationRule
    {
        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
            => throw new FormatException("rule blew up");
    }

    // Throws from MoveNext rather than from Validate, so the first finding is already in hand.
    private sealed class PartiallyThrowingRule : IRequestFlowValidationRule
    {
        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
        {
            yield return new RequestFlowValidationProblem("TEST0004", "found before the throw");

            throw new NotSupportedException("enumeration blew up");
        }
    }

    // Its message copies RequestFlow's own null-sequence diagnostic word for word.
    private sealed class LookalikeThrowingRule : IRequestFlowValidationRule
    {
        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
            => throw new InvalidOperationException(
                $"Validation rule '{typeof(LookalikeThrowingRule).FullName}' returned null instead of an empty sequence.");
    }

    private sealed class NullReturningRule : IRequestFlowValidationRule
    {
        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
            => null!;
    }

    private sealed class NullProblemReturningRule : IRequestFlowValidationRule
    {
        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
            => [null!];
    }

    private sealed class EventFactsRule : IRequestFlowValidationRule
    {
        public IEnumerable<RequestFlowValidationProblem> Validate(
            RequestFlowValidationContext context)
        {
            bool hasEvent = context.Model.Events.Any(@event =>
                @event.EventType
                    == typeof(RequestFlow.Tests.ValidationFixtures.ExternalContractEvent));
            bool hasSubscription = context.Model.EventSubscriptions.Any(subscription =>
                subscription.HandlerType == typeof(ExternalContractEventHandler));

            yield return new RequestFlowValidationProblem(
                "TEST0005",
                $"event={hasEvent} subscription={hasSubscription} " +
                $"unhandled={context.AllUnhandledEventsAllowed} " +
                $"unused={context.UnusedEventHandlersDisallowed}",
                typeof(RequestFlow.Tests.ValidationFixtures.ExternalContractEvent));
        }
    }

    #endregion
}
