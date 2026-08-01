// Startup only: nothing here runs on the dispatch path.

using System;

namespace RequestFlow;

/// <summary>
/// Decides whether one stage declaration applies to one handler registration and, when it
/// does, produces the closed stage type. This is the single source of applicability:
/// registration emits closed service descriptors and the freeze orders each request's stages
/// from the same answers, cached per pair in <see cref="StageClosingCache"/>, so a provider
/// built after the last <c>AddRequestFlow</c> call never sees the two disagree.
/// </summary>
internal static class StageClosing
{
    /// <summary>
    /// Applicability without the explanation.
    /// </summary>
    public static bool TryClose(StageDeclaration declaration, HandlerRegistration handler, out Type closedStageType)
        => TryClose(declaration, handler, out closedStageType, out _);

    /// <summary>
    /// True when <paramref name="declaration"/> applies to <paramref name="handler"/>, with
    /// <paramref name="closedStageType"/> set to the type to resolve and
    /// <paramref name="reason"/> to why it applies. Both out parameters are left at their
    /// defaults when it does not.
    /// </summary>
    public static bool TryClose(
        StageDeclaration declaration,
        HandlerRegistration handler,
        out Type closedStageType,
        out string reason)
    {
        closedStageType = null!;
        reason = string.Empty;

        if (declaration.HandlerFilter is not null
            && !declaration.HandlerFilter.IsAssignableFrom(handler.ImplementationType))
            return false;

        Type stageType = declaration.StageType;
        bool isOpen = stageType.IsGenericTypeDefinition;
        Type candidate;

        if (isOpen)
        {
            // A one-parameter definition is the void form, which names the request only.
            Type[] arguments = stageType.GetGenericArguments().Length == 1
                ? [handler.RequestType]
                : [handler.RequestType, handler.ResponseType];

            try
            {
                candidate = stageType.MakeGenericType(arguments);
            }
            catch (ArgumentException)
            {
                // Generic constraints are how a stage states which requests it applies to, so
                // excluding this one is an answer, not a registration problem.
                return false;
            }
        }
        else
        {
            candidate = stageType;
        }

        if (!SatisfiesContract(candidate, handler))
            return false;

        reason = isOpen
            ? $"generic constraints admit {Describe(handler)}"
            : $"closed stage declared for {Describe(handler)}";

        if (declaration.HandlerFilter is not null)
            reason += $", handler implements {declaration.HandlerFilter.Name}";

        closedStageType = candidate;
        return true;
    }

    // Honors the in TRequest variance, so a closed stage written against a base request type
    // also applies to requests that inherit the contract. The void form is only ever offered
    // to a void handler, since that is the only place its response type can line up.
    private static bool SatisfiesContract(Type candidate, HandlerRegistration handler)
    {
        Type contract = typeof(IRequestStage<,>).MakeGenericType(handler.RequestType, handler.ResponseType);
        if (contract.IsAssignableFrom(candidate))
            return true;

        if (!handler.IsVoid)
            return false;

        Type voidContract = typeof(IRequestStage<>).MakeGenericType(handler.RequestType);
        return voidContract.IsAssignableFrom(candidate);
    }

    private static string Describe(HandlerRegistration handler)
        => $"{handler.RequestType.Name} -> {handler.ResponseType.Name}";
}
