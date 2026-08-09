using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Runs one freeze's validation rules and collects what they report.
/// </summary>
/// <remarks>
/// A built-in rule runs unguarded, so a bug in RequestFlow surfaces as the exception it threw. A
/// registered rule runs guarded, so its failure becomes one more problem and the pass keeps going.
/// </remarks>
internal static class ValidationRuleRunner
{
    /// <summary>
    /// Collects <paramref name="seeded"/> plus everything the rules report, in that order.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public static List<RequestFlowValidationProblem> Run(
        RequestFlowValidationContext context,
        IEnumerable<IRequestFlowValidationRule> builtInRules,
        IEnumerable<IRequestFlowValidationRule> registeredRules,
        IReadOnlyList<RequestFlowValidationProblem> seeded)
    {
        // Copied, not appended to: the registry keeps its scan-time problems for the next provider built from the same service collection.
        List<RequestFlowValidationProblem> problems = [.. seeded];

        foreach (var rule in builtInRules)
            Collect(rule, context, problems);

        // Last, so a third-party finding never sits between two of RequestFlow's own.
        foreach (var rule in registeredRules)
            RunGuarded(rule, context, problems);

        return problems;
    }

    // Guards a registered rule. Returning null, or a null problem, is a bug in the rule and still
    // throws. An exception from its own code becomes one more problem, so the pass keeps going.
    private static void RunGuarded(
        IRequestFlowValidationRule rule,
        RequestFlowValidationContext context,
        List<RequestFlowValidationProblem> problems)
    {
        // Findings reach the report only once the rule finishes, so a rule that throws halfway
        // leaves none behind.
        List<RequestFlowValidationProblem> reported = [];
        try
        {
            Collect(rule, context, reported);
        }
        catch (RuleContractException contract)
        {
            throw new InvalidOperationException(contract.Message);
        }
        catch (Exception exception)
        {
            // The stack trace goes, every other finding stays.
            problems.Add(new RequestFlowValidationProblem(
                ProblemCodes.RuleFailed,
                $"Validation rule '{rule.GetType().FullName}' threw {exception.GetType().FullName}: " +
                $"'{exception.Message}'. Its findings were dropped and the other rules still ran; " +
                "catch inside the rule and report a problem instead.",
                rule.GetType()));

            return;
        }

        problems.AddRange(reported);
    }

    private static void Collect(
        IRequestFlowValidationRule rule,
        RequestFlowValidationContext context,
        List<RequestFlowValidationProblem> reported)
    {
        IEnumerable<RequestFlowValidationProblem>? sequence = rule.Validate(context);
        if (sequence is null)
        {
            throw new RuleContractException(
                $"Validation rule '{rule.GetType().FullName}' returned null instead of an empty sequence.");
        }

        foreach (var problem in sequence)
        {
            if (problem is null)
            {
                throw new RuleContractException(
                    $"Validation rule '{rule.GetType().FullName}' returned a null problem.");
            }

            reported.Add(problem);
        }
    }

    /// <summary>
    /// Marks the two diagnostics RequestFlow raises about a validation rule that returns null,
    /// keeping them apart from an exception the rule's own code threw.
    /// </summary>
    private sealed class RuleContractException(string message)
        : Exception(message)
    { }
}
