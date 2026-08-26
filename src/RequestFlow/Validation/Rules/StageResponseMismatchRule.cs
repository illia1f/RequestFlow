using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reports a stage whose fixed response type is not the one its request declares.
/// </summary>
/// <remarks>
/// The task twin of <see cref="StreamStageItemMismatchRule"/>. The stage contract's constraint
/// accepts a wider response because <c>IRequest&lt;TResponse&gt;</c> is covariant, but closing is
/// invariant in the response, so such a stage compiles and then wraps no handler.
/// Only <see cref="UnusedStageRule"/> would notice, and only behind <c>DisallowUnusedStages</c>, so
/// this rule runs unconditionally for a stage that wrapped nothing. A stage that reached any
/// request is not reported, since skipping the rest can be deliberate scoping.
/// <para>
/// A filtered call wraps nothing when its filter admits no handler, which says nothing about its
/// response type, so only requests with a handler the filter admits are examined. The filter and the
/// declaration's own family come from <paramref name="facts"/>, since the model holds neither.
/// </para>
/// <para>
/// A void stage names no response and cannot mismatch, so it falls out on its own: nothing it
/// implements is <c>IRequestStage&lt;TRequest, TResponse&gt;</c>.
/// </para>
/// </remarks>
/// <param name="facts">
/// What the calls that registered these stages named. Null for a model built by hand, where the
/// recorded contract answers instead.
/// </param>
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

            // A two-parameter definition closes over the handler's own response type, so it cannot
            // mismatch; the one-parameter form fixes the response in the class, so it can. A stage
            // that wrapped a handler somewhere is scoped, not trapped.
            if ((stageType.IsGenericTypeDefinition && stageType.GetGenericArguments().Length != 1)
                || _facts.GetFamily(stageType, declaration.ContractType) != StageFamily.Request
                || declaration.ReachedRequests.Count > 0
                || !checkedStages.Add(stageType))
                continue;

            Type? handlerFilter = _facts.GetHandlerFilter(stageType);

            foreach (var request in context.Model.Requests)
            {
                // A filter the request's handlers fail is why this stage skipped it, whatever its response type.
                if (handlerFilter is not null && !Admits(handlerFilter, request))
                    continue;

                // No sole contract, no response type to hold the stage to; RF0106 reports the ambiguity.
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

    private static bool Admits(Type handlerFilter, RequestModel request)
    {
        foreach (var handler in request.Handlers)
        {
            if (handlerFilter.IsAssignableFrom(handler.HandlerType))
                return true;
        }

        return false;
    }

    // The closed type the freeze would test, or null when generic constraints exclude the
    // request; exclusion is an answer, not a mismatch.
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
