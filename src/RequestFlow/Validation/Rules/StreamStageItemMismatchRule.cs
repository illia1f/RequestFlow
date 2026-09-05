using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Rejects stream stages whose fixed item type differs from the request contract.
/// </summary>
/// <remarks>
/// Covariance allows wider item types to compile, but stage closing requires an exact match.
/// Only stages that reached no handler in this family are checked.
/// A request is checked when any declaration's filter admits one of its handlers.
/// <paramref name="facts"/> supplies filters; when null, the model's recorded contract is used.
/// </remarks>
internal sealed class StreamStageItemMismatchRule(StageDeclarationFacts? facts = null)
    : IRequestFlowValidationRule
{
    private readonly StageDeclarationFacts _facts = facts ?? StageDeclarationFacts.None;

    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        // A stage type declared twice is one mismatch, not two; RF0103 reports the duplicate.
        HashSet<Type> checkedStages = [];

        Dictionary<Type, Type?> declaredItems = [];

        foreach (var declaration in context.Model.StageDeclarations)
        {
            Type stageType = declaration.StageType;

            // Two-parameter stages use the handler's item type; one-parameter stages can fix a wider type.
            // A stage that reached any handler in this family is treated as deliberately scoped.
            if ((stageType.IsGenericTypeDefinition && stageType.GetGenericArguments().Length != 1)
                || !_facts.HasFamily(stageType, StageFamily.Stream, declaration.ContractType)
                || _facts.AnyDeclarationReached(
                    stageType,
                    StageFamily.Stream,
                    declaration.ReachedRequests.Count > 0)
                || !checkedStages.Add(stageType))
                continue;

            foreach (var request in context.Model.Requests)
            {
                // A request excluded by every declaration's filter says nothing about the item type.
                if (!_facts.AnyDeclarationAdmits(stageType, StageFamily.Stream, request))
                    continue;

                // RF0108 or a pairwise conflict owns a request with no sole stream item.
                Type? declaredItem = GetSoleDeclaredItem(request.RequestType, declaredItems);
                if (declaredItem is null)
                    continue;

                Type? candidate = CloseOver(stageType, request.RequestType);
                if (candidate is null)
                    continue;

                Type? mismatchedItem = null;

                foreach (var iface in candidate.GetInterfaces())
                {
                    if (!iface.IsGenericType
                        || iface.GetGenericTypeDefinition() != typeof(IStreamRequestStage<,>))
                        continue;

                    Type[] arguments = iface.GetGenericArguments();

                    // TRequest is contravariant, so a stage written against a base request also
                    // aims at every request deriving from it.
                    if (!arguments[0].IsAssignableFrom(request.RequestType))
                        continue;

                    if (arguments[1] == declaredItem)
                    {
                        mismatchedItem = null;
                        break;
                    }

                    mismatchedItem ??= arguments[1];
                }

                if (mismatchedItem is null)
                    continue;

                yield return new RequestFlowValidationProblem(
                    ProblemCodes.StreamStageItemMismatch,
                    $"Stream stage '{stageType.FullName}' takes '{mismatchedItem.FullName}' items for request " +
                    $"'{request.RequestType.FullName}', which declares IStreamRequest<{declaredItem.FullName}>; " +
                    "a stage closes over the declared item type, so this stage would never run. Give the stage " +
                    "the declared item type.",
                    stageType);
            }
        }
    }

    // Constraint rejection excludes the request; it is not an item mismatch.
    private static Type? CloseOver(Type stageType, Type requestType)
    {
        if (!stageType.IsGenericTypeDefinition)
            return stageType;

        try
        {
            return stageType.MakeGenericType(requestType);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // Cache results, including null, per request type for one validation pass.
    private static Type? GetSoleDeclaredItem(Type requestType, Dictionary<Type, Type?> memo)
    {
        if (memo.TryGetValue(requestType, out Type? cached))
            return cached;

        Type? declared = GetSoleDeclaredItem(requestType);
        memo[requestType] = declared;

        return declared;
    }

    private static Type? GetSoleDeclaredItem(Type requestType)
    {
        Type? declared = null;

        foreach (var iface in requestType.GetInterfaces())
        {
            if (!iface.IsGenericType)
                continue;

            Type definition = iface.GetGenericTypeDefinition();
            if (definition == typeof(IRequest<>) || definition == typeof(IValueRequest<>))
                return null;

            if (definition != typeof(IStreamRequest<>))
                continue;

            if (declared is not null)
                return null;

            declared = iface.GetGenericArguments()[0];
        }

        return declared;
    }
}
