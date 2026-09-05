using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Rejects stages whose fixed response type differs from the request contract.
/// </summary>
/// <remarks>
/// Covariance allows wider responses to compile, but stage closing requires an exact match.
/// Only stages that reached no handler in this family are checked.
/// A request is checked when any declaration's filter admits one of its handlers.
/// <paramref name="facts"/> supplies filters; when null, the model's recorded contract is used.
/// </remarks>
internal sealed class StageResponseMismatchRule(StageDeclarationFacts? facts = null)
    : IRequestFlowValidationRule
{
    private readonly StageDeclarationFacts _facts = facts ?? StageDeclarationFacts.None;

    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        // A stage type declared twice is one mismatch, not two; RF0103 reports the duplicate.
        HashSet<Type> checkedStages = [];

        Dictionary<Type, Type?> declaredResponses = [];

        foreach (var declaration in context.Model.StageDeclarations)
        {
            Type stageType = declaration.StageType;

            // Two-parameter stages use the handler's response; one-parameter stages can fix a wider type.
            // A stage that reached any handler in this family is treated as deliberately scoped.
            if ((stageType.IsGenericTypeDefinition && stageType.GetGenericArguments().Length != 1)
                || !_facts.HasFamily(stageType, StageFamily.Request, declaration.ContractType)
                || _facts.AnyDeclarationReached(
                    stageType,
                    StageFamily.Request,
                    declaration.ReachedRequests.Count > 0)
                || !checkedStages.Add(stageType))
                continue;

            foreach (var request in context.Model.Requests)
            {
                // A request excluded by every declaration's filter says nothing about the response type.
                if (!_facts.AnyDeclarationAdmits(stageType, StageFamily.Request, request))
                    continue;

                // RF0106 or a pairwise conflict owns a request with no sole Task response.
                Type? declaredResponse =
                    RequestContracts.GetSoleDeclaredResponse(request.RequestType, declaredResponses);
                if (declaredResponse is null)
                    continue;

                Type? candidate = CloseOver(stageType, request.RequestType);
                if (candidate is null)
                    continue;

                Type? mismatchedResponse = null;

                foreach (var iface in candidate.GetInterfaces())
                {
                    if (!iface.IsGenericType
                        || iface.GetGenericTypeDefinition() != typeof(IRequestStage<,>))
                        continue;

                    Type[] arguments = iface.GetGenericArguments();

                    // TRequest is contravariant, so a stage written against a base request also
                    // aims at every request deriving from it.
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
                    ProblemCodes.StageResponseMismatch,
                    $"Stage '{stageType.FullName}' takes '{mismatchedResponse.FullName}' for request " +
                    $"'{request.RequestType.FullName}', which declares IRequest<{declaredResponse.FullName}>; " +
                    "a stage closes over the declared response type, so this stage would never run. Give the " +
                    "stage the declared response type.",
                    stageType);
            }
        }
    }

    // Constraint rejection excludes the request; it is not a response mismatch.
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
