using System;

namespace RequestFlow;

/// <summary>
/// Closes stage declarations over handler registrations for registration and freeze.
/// </summary>
internal static class StageClosing
{
    public static bool TryClose(StageDeclaration declaration, HandlerRegistration handler, out Type closedStageType)
    {
        StageClosingResult result = Match(declaration, handler);
        closedStageType = result.ClosedType!;
        return result.ClosedType is not null;
    }

    /// <summary>
    /// Returns whether the stage applies, with its closed type and matching reason.
    /// Both output parameters keep their defaults when it does not apply.
    /// </summary>
    public static bool TryClose(
        StageDeclaration declaration,
        HandlerRegistration handler,
        out Type closedStageType,
        out string reason)
    {
        reason = string.Empty;
        if (!TryClose(declaration, handler, out closedStageType))
            return false;

        reason = declaration.StageType.IsGenericTypeDefinition
            ? $"generic constraints admit {Describe(handler)}"
            : $"closed stage declared for {Describe(handler)}";

        if (declaration.HandlerFilter is not null)
            reason += $", handler implements {declaration.HandlerFilter.Name}";

        return true;
    }

    public static StageClosingResult Match(StageDeclaration declaration, HandlerRegistration handler)
    {
        // A declaration closes only against its Task, ValueTask, or stream handler family.
        if (!declaration.Family.Handles(handler.ContractDefinition))
            return new StageClosingResult(null, StageExclusionReason.DifferentFamily);

        if (declaration.HandlerFilter is not null
            && !declaration.HandlerFilter.IsAssignableFrom(handler.ImplementationType))
            return new StageClosingResult(null, StageExclusionReason.HandlerFilterNotMatched);

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
                // Generic constraints filter requests; a rejected request is not a registration error.
                return new StageClosingResult(null, StageExclusionReason.GenericConstraintsNotSatisfied);
            }
        }
        else
        {
            candidate = stageType;
        }

        if (!SatisfiesContract(candidate, declaration.Family, handler))
            return new StageClosingResult(null, StageExclusionReason.ContractNotCompatible);

        return new StageClosingResult(candidate, null);
    }

    // Honors the in TRequest variance, so a closed stage written against a base request type
    // also applies to requests that inherit the contract.
    private static bool SatisfiesContract(Type candidate, StageFamily family, HandlerRegistration handler)
    {
        Type contract = family.TypedContract.MakeGenericType(handler.RequestType, handler.ResponseType);
        if (contract.IsAssignableFrom(candidate))
            return true;

        if (family.VoidContract is null || !handler.IsVoid)
            return false;

        Type voidContract = family.VoidContract.MakeGenericType(handler.RequestType);
        return voidContract.IsAssignableFrom(candidate);
    }

    private static string Describe(HandlerRegistration handler)
        => $"{handler.RequestType.Name} -> {handler.ResponseType.Name}";
}

internal readonly struct StageClosingResult(Type? closedType, StageExclusionReason? exclusionReason)
{
    public Type? ClosedType { get; } = closedType;

    public StageExclusionReason? ExclusionReason { get; } = exclusionReason;
}
