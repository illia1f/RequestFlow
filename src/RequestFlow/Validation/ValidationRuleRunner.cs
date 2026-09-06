using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Runs one freeze's validation rules and throws if any report a problem.
/// </summary>
/// <remarks>
/// A built-in rule runs unguarded, so a bug in RequestFlow surfaces as the exception it threw.
/// A registered rule runs guarded, so its failure becomes one more problem and the pass keeps going.
/// </remarks>
internal static class ValidationRuleRunner
{
    /// <summary>
    /// Reports <paramref name="seeded"/> followed by built-in and registered rule findings.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    /// <exception cref="RequestFlowValidationException"/>
    public static void Validate(
        RequestFlowValidationContext context,
        IEnumerable<IRequestFlowValidationRule> builtInRules,
        IEnumerable<IRequestFlowValidationRule> registeredRules,
        IReadOnlyList<RequestFlowValidationProblem> seeded)
    {
        List<ValidationFinding> findings = [];
        foreach (var problem in seeded)
            findings.Add(new ValidationFinding(problem));

        foreach (var rule in builtInRules)
            findings.AddRange(Collect(rule, context));

        foreach (var rule in registeredRules)
            findings.AddRange(RunGuarded(rule, context));

        ThrowIfInvalid(findings);
    }

    // Guards a registered rule. Returning null, or a null problem, is a bug in the rule and still
    // throws. An exception from its own code becomes one more problem, so the pass keeps going.
    private static IReadOnlyList<ValidationFinding> RunGuarded(
        IRequestFlowValidationRule rule,
        RequestFlowValidationContext context)
    {
        try
        {
            return Collect(rule, context);
        }
        catch (RuleContractException contract)
        {
            throw new InvalidOperationException(contract.Message);
        }
        catch (Exception exception)
        {
            var problem = new RequestFlowValidationProblem(
                ProblemCodes.RuleFailed,
                $"Validation rule '{rule.GetType().FullName}' threw {exception.GetType().FullName}: '{exception.Message}'. " +
                "Its findings were dropped and the other rules still ran; catch inside the rule and report a problem instead.",
                rule.GetType());

            return [new ValidationFinding(problem, exception)];
        }
    }

    private static IReadOnlyList<ValidationFinding> Collect(
        IRequestFlowValidationRule rule,
        RequestFlowValidationContext context)
    {
        IEnumerable<RequestFlowValidationProblem>? sequence = rule.Validate(context);
        if (sequence is null)
        {
            throw new RuleContractException(
                $"Validation rule '{rule.GetType().FullName}' returned null instead of an empty sequence.");
        }

        List<ValidationFinding> findings = [];
        foreach (var problem in sequence)
        {
            if (problem is null)
            {
                throw new RuleContractException(
                    $"Validation rule '{rule.GetType().FullName}' returned a null problem.");
            }

            findings.Add(new ValidationFinding(problem));
        }

        return findings;
    }

    private static void ThrowIfInvalid(IReadOnlyList<ValidationFinding> findings)
    {
        if (findings.Count == 0)
            return;

        List<RequestFlowValidationProblem> problems = [];
        List<Exception> exceptions = [];
        foreach (var finding in findings)
        {
            problems.Add(finding.Problem);
            if (finding.Cause is not null)
                exceptions.Add(finding.Cause);
        }

        AggregateException? innerException = exceptions.Count == 0
            ? null
            : new AggregateException("RequestFlow validation rules threw exceptions.", exceptions);

        throw new RequestFlowValidationException(problems, innerException);
    }

    private readonly record struct ValidationFinding(RequestFlowValidationProblem Problem, Exception? Cause = null);

    /// <summary>
    /// Marks the two diagnostics RequestFlow raises about a validation rule that returns null,
    /// keeping them apart from an exception the rule's own code threw.
    /// </summary>
    private sealed class RuleContractException(string message)
        : Exception(message)
    { }
}
