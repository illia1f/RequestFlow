using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Detects registration problems. The shape checks live here; the freeze-time checks live in
/// <c>Validation/</c> rules.
/// </summary>
internal static class RegistrationValidator
{
    /// <summary>
    /// Shape checks per declaration, stopping at that declaration's first failure.
    /// </summary>
    public static Validated<GenericHandlerDeclaration> ValidateDeclarations(
        IReadOnlyList<GenericHandlerDeclaration> declarations)
        => Partition(declarations, ValidateDeclaration);

    private static RequestFlowValidationProblem? ValidateDeclaration(GenericHandlerDeclaration declaration)
    {
        Type handlerType = declaration.HandlerType;

        if (!handlerType.IsGenericTypeDefinition)
            return new RequestFlowValidationProblem(
                ProblemCodes.HandlerNotOpenGeneric,
                $"'{handlerType.FullName}' is not an open generic type definition; pass e.g. typeof(AuditHandler<>).",
                handlerType);

        if (handlerType.IsAbstract)
            return new RequestFlowValidationProblem(
                ProblemCodes.HandlerAbstract,
                $"'{handlerType.FullName}' is abstract; only concrete handler classes can be registered.",
                handlerType);

        if (handlerType.GetGenericArguments().Length != 1)
            return new RequestFlowValidationProblem(
                ProblemCodes.HandlerWrongArity,
                $"'{handlerType.FullName}' has {handlerType.GetGenericArguments().Length} generic parameters; only single-parameter generic handlers are supported.",
                handlerType);

        if (!ImplementsHandlerContract(handlerType))
            return new RequestFlowValidationProblem(
                ProblemCodes.HandlerMissingContract,
                $"'{handlerType.FullName}' does not implement IRequestHandler or IStreamRequestHandler.",
                handlerType);

        if (declaration.ClosingTypes.Length == 0)
            return new RequestFlowValidationProblem(
                ProblemCodes.NoClosingTypes,
                $"Generic handler '{handlerType.FullName}' declares no closing types; at least one is required.",
                handlerType);

        foreach (var closingType in declaration.ClosingTypes)
        {
            if (closingType.ContainsGenericParameters)
                return new RequestFlowValidationProblem(
                    ProblemCodes.ClosingTypeNotClosed,
                    $"Closing type '{closingType.FullName}' for generic handler '{handlerType.FullName}' is not a closed type.",
                    closingType);
        }

        return null;
    }

    private static bool ImplementsHandlerContract(Type handlerType)
    {
        foreach (var iface in handlerType.GetInterfaces())
        {
            if (!iface.IsGenericType)
                continue;

            Type definition = iface.GetGenericTypeDefinition();
            if (definition == typeof(IRequestHandler<,>)
                || definition == typeof(IRequestHandler<>)
                || definition == typeof(IStreamRequestHandler<,>))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Closes each handler over its declared type; closings that violate the handler's
    /// generic constraints are reported as problems.
    /// </summary>
    public static Validated<Type> ValidateClosings(IReadOnlyList<GenericHandlerClosing> closings)
    {
        List<Type> closedTypes = [];
        List<RequestFlowValidationProblem> problems = [];

        foreach (var closing in closings)
        {
            try
            {
                closedTypes.Add(closing.HandlerType.MakeGenericType(closing.ClosingType));
            }
            catch (ArgumentException)
            {
                problems.Add(new RequestFlowValidationProblem(
                    ProblemCodes.ClosingViolatesConstraints,
                    $"Generic handler '{closing.HandlerType.FullName}' cannot be closed over " +
                    $"'{closing.ClosingType.FullName}'; the type argument violates the handler's generic constraints.",
                    closing.HandlerType));
            }
        }

        return new Validated<Type>(closedTypes, problems);
    }

    /// <summary>
    /// Checks each stage declaration's shape, stopping at that declaration's first failure.
    /// </summary>
    public static Validated<StageDeclaration> ValidateStageDeclarations(IReadOnlyList<StageDeclaration> declarations)
        => Partition(declarations, ValidateStageDeclaration);

    private static RequestFlowValidationProblem? ValidateStageDeclaration(StageDeclaration declaration)
    {
        Type stageType = declaration.StageType;

        if (stageType.IsInterface)
            return new RequestFlowValidationProblem(
                ProblemCodes.StageIsInterface,
                $"'{stageType.FullName}' is an interface; only concrete stage classes can be registered.",
                stageType);

        if (stageType.IsAbstract)
            return new RequestFlowValidationProblem(
                ProblemCodes.StageAbstract,
                $"'{stageType.FullName}' is abstract; only concrete stage classes can be registered.",
                stageType);

        if (!stageType.IsGenericTypeDefinition && stageType.ContainsGenericParameters)
            return new RequestFlowValidationProblem(
                ProblemCodes.StagePartiallyClosed,
                $"'{stageType.FullName}' is partially closed; register either the open generic definition " +
                "or a fully closed stage type.",
                stageType);

        if (!ImplementsStageContract(stageType, declaration.Family))
            return new RequestFlowValidationProblem(
                ProblemCodes.StageMissingContract,
                declaration.Family.MissingContractMessage(stageType),
                stageType);

        if (stageType.IsGenericTypeDefinition && !ClosesOverItsOwnParameters(stageType, declaration.Family))
        {
            string parameterNames = string.Join(
                ", ", Array.ConvertAll(stageType.GetGenericArguments(), static p => p.Name));

            return new RequestFlowValidationProblem(
                ProblemCodes.StageParametersMisused,
                declaration.Family.ParametersMisusedMessage(stageType, parameterNames),
                stageType);
        }

        return null;
    }

    private static bool ImplementsStageContract(Type stageType, StageFamily family)
    {
        foreach (var iface in stageType.GetInterfaces())
        {
            if (!iface.IsGenericType)
                continue;

            if (IsFamilyContract(iface.GetGenericTypeDefinition(), family))
                return true;
        }

        return false;
    }

    // MakeGenericType substitutes positionally and StageClosing closes a one-parameter
    // definition over the request alone, so the interface's request argument has to be the
    // stage's own parameter for the closed type to name the dispatched request. A stage that
    // breaks this closes into a type no request can match.
    private static bool ClosesOverItsOwnParameters(Type stageType, StageFamily family)
    {
        Type[] parameters = stageType.GetGenericArguments();

        foreach (var iface in stageType.GetInterfaces())
        {
            if (!iface.IsGenericType)
                continue;

            if (!IsFamilyContract(iface.GetGenericTypeDefinition(), family))
                continue;

            Type[] arguments = iface.GetGenericArguments();

            if (parameters.Length == arguments.Length && SubstitutePositionally(parameters, arguments))
                return true;

            // One parameter naming the request: the void form, or the general form with the second
            // argument fixed by the class, as in Stage<TRequest> : IRequestStage<TRequest, Result>.
            if (parameters.Length == 1 && arguments[0] == parameters[0])
                return true;
        }

        return false;
    }

    private static bool IsFamilyContract(Type definition, StageFamily family)
    {
        foreach (var contract in family.Contracts)
        {
            if (definition == contract)
                return true;
        }

        return false;
    }

    private static bool SubstitutePositionally(Type[] parameters, Type[] arguments)
    {
        for (int i = 0; i < parameters.Length; i++)
        {
            if (arguments[i] != parameters[i])
                return false;
        }

        return true;
    }

    private static Validated<T> Partition<T>(
        IReadOnlyList<T> items, Func<T, RequestFlowValidationProblem?> validate)
    {
        List<T> valid = [];
        List<RequestFlowValidationProblem> problems = [];

        foreach (var item in items)
        {
            RequestFlowValidationProblem? problem = validate(item);
            if (problem is null)
                valid.Add(item);
            else
                problems.Add(problem);
        }

        return new Validated<T>(valid, problems);
    }
}

/// <summary>
/// The items a validation pass produced, plus one problem for each item it rejected.
/// </summary>
internal sealed class Validated<T>(IReadOnlyList<T> valid, IReadOnlyList<RequestFlowValidationProblem> problems)
{
    public IReadOnlyList<T> Valid { get; } = valid;

    public IReadOnlyList<RequestFlowValidationProblem> Problems { get; } = problems;
}
