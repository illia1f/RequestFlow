using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reports a ValueTask stage whose fixed response type differs from its request contract.
/// </summary>
internal sealed class ValueStageResponseMismatchRule(StageDeclarationFacts? facts = null)
    : IRequestFlowValidationRule
{
    private readonly StageDeclarationFacts _facts = facts ?? StageDeclarationFacts.None;

    public IEnumerable<RequestFlowValidationProblem> Validate(
        RequestFlowValidationContext context)
    {
        HashSet<Type> checkedStages = [];
        Dictionary<Type, Type?> declaredResponses = [];

        foreach (StageDeclarationModel declaration in context.Model.StageDeclarations)
        {
            Type stageType = declaration.StageType;

            if ((stageType.IsGenericTypeDefinition && stageType.GetGenericArguments().Length != 1)
                || !_facts.HasFamily(stageType, StageFamily.Value, declaration.ContractType)
                || _facts.AnyDeclarationReached(
                    stageType,
                    StageFamily.Value,
                    declaration.ReachedRequests.Count > 0)
                || !checkedStages.Add(stageType))
            {
                continue;
            }

            foreach (RequestModel request in context.Model.Requests)
            {
                if (!_facts.AnyDeclarationAdmits(stageType, StageFamily.Value, request))
                    continue;

                Type? declaredResponse = ValueRequestContracts.GetSoleDeclaredResponse(
                    request.RequestType,
                    declaredResponses);
                if (declaredResponse is null)
                    continue;

                Type? candidate = CloseOver(stageType, request.RequestType);
                if (candidate is null)
                    continue;

                Type? mismatchedResponse = null;

                foreach (Type iface in candidate.GetInterfaces())
                {
                    if (!iface.IsGenericType
                        || iface.GetGenericTypeDefinition() != typeof(IValueRequestStage<,>))
                    {
                        continue;
                    }

                    Type[] arguments = iface.GetGenericArguments();
                    if (!arguments[0].IsAssignableFrom(request.RequestType))
                        continue;

                    if (arguments[1] == declaredResponse)
                    {
                        mismatchedResponse = null;
                        break;
                    }

                    mismatchedResponse ??= arguments[1];
                }

                if (mismatchedResponse is null)
                    continue;

                yield return new RequestFlowValidationProblem(
                    ProblemCodes.ValueStageResponseMismatch,
                    $"ValueTask stage '{stageType.FullName}' takes '{mismatchedResponse.FullName}' for request " +
                    $"'{request.RequestType.FullName}', which declares IValueRequest<{declaredResponse.FullName}>; " +
                    "a stage closes over the declared response type, so this stage would never run. Give the " +
                    "stage the declared response type.",
                    stageType);
            }
        }
    }

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
}
