namespace RequestFlow.Tests.Unit.Validation;

public sealed class ValidationRuleRunnerTests
{
    [Fact]
    public void Given_Seeded_Problems_When_Validating_Twice_Then_Rule_Findings_Do_Not_Accumulate_In_The_Seed()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder().BuildContext();
        var seededProblem = new RequestFlowValidationProblem("TEST0001", "scan failure");
        var ruleProblem = new RequestFlowValidationProblem("TEST0002", "rule finding");
        List<RequestFlowValidationProblem> seeded = [seededProblem];
        var rule = Substitute.For<IRequestFlowValidationRule>();
        rule.Validate(context).Returns([ruleProblem]);

        RequestFlowValidationException first = Should.Throw<RequestFlowValidationException>(() =>
            ValidationRuleRunner.Validate(context, [], [rule], seeded));
        RequestFlowValidationException second = Should.Throw<RequestFlowValidationException>(() =>
            ValidationRuleRunner.Validate(context, [], [rule], seeded));

        seeded.ShouldHaveSingleItem().ShouldBeSameAs(seededProblem);
        first.Problems.ShouldBe([seededProblem, ruleProblem]);
        second.Problems.ShouldBe([seededProblem, ruleProblem]);
        first.InnerException.ShouldBeNull();
        second.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Given_A_Throwing_Built_In_Rule_When_Validating_Then_Its_Exception_Propagates_Unwrapped()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder().BuildContext();
        var failure = new FormatException("built-in failure");
        var rule = Substitute.For<IRequestFlowValidationRule>();
        rule.Validate(context).Returns(_ => throw failure);

        FormatException exception = Should.Throw<FormatException>(() =>
            ValidationRuleRunner.Validate(context, [rule], [], []));

        exception.ShouldBeSameAs(failure);
    }

    [Fact]
    public void Given_A_Rule_Whose_Enumerator_Throws_On_Dispose_When_Validating_Then_Its_Findings_Are_Replaced_By_Its_Failure()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder().BuildContext();
        var failure = new FormatException("dispose failure");
        var enumerator = Substitute.For<IEnumerator<RequestFlowValidationProblem>>();
        enumerator.MoveNext().Returns(true, false);
        enumerator.Current.Returns(new RequestFlowValidationProblem("TEST0001", "discarded finding"));
        enumerator.When(e => e.Dispose()).Do(_ => throw failure);
        var sequence = Substitute.For<IEnumerable<RequestFlowValidationProblem>>();
        sequence.GetEnumerator().Returns(enumerator);
        var throwingRule = Substitute.For<IRequestFlowValidationRule>();
        throwingRule.Validate(context).Returns(sequence);
        var laterProblem = new RequestFlowValidationProblem("TEST0002", "later finding");
        var laterRule = Substitute.For<IRequestFlowValidationRule>();
        laterRule.Validate(context).Returns([laterProblem]);

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            ValidationRuleRunner.Validate(context, [], [throwingRule, laterRule], []));

        exception.Problems.Count.ShouldBe(2);
        exception.Problems[0].Code.ShouldBe(ProblemCodes.RuleFailed);
        exception.Problems[0].Subject.ShouldBe(throwingRule.GetType());
        exception.Problems[1].ShouldBeSameAs(laterProblem);
        AggregateException aggregate = exception.InnerException.ShouldBeOfType<AggregateException>();
        aggregate.InnerExceptions.ShouldHaveSingleItem().ShouldBeSameAs(failure);
    }
}
