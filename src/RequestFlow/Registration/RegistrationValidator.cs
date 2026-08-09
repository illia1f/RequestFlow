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
    public static DeclarationResult ValidateDeclarations(IReadOnlyList<GenericHandlerDeclaration> declarations)
    {
        List<GenericHandlerDeclaration> validDeclarations = [];
        List<RequestFlowValidationProblem> problems = [];

        foreach (var declaration in declarations)
        {
            RequestFlowValidationProblem? problem = ValidateDeclaration(declaration);
            if (problem is null)
                validDeclarations.Add(declaration);
            else
                problems.Add(problem);
        }

        return new DeclarationResult(validDeclarations, problems);
    }

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
                $"'{handlerType.FullName}' does not implement IRequestHandler.",
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
            if (definition == typeof(IRequestHandler<,>) || definition == typeof(IRequestHandler<>))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Closes each handler over its declared type; closings that violate the handler's
    /// generic constraints are reported as problems.
    /// </summary>
    public static ClosingResult ValidateClosings(IReadOnlyList<GenericHandlerClosing> closings)
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

        return new ClosingResult(closedTypes, problems);
    }

    /// <summary>
    /// Checks each stage declaration's shape, stopping at that declaration's first failure.
    /// </summary>
    public static StageDeclarationResult ValidateStageDeclarations(IReadOnlyList<StageDeclaration> declarations)
    {
        List<StageDeclaration> validDeclarations = [];
        List<RequestFlowValidationProblem> problems = [];

        foreach (var declaration in declarations)
        {
            RequestFlowValidationProblem? problem = ValidateStageDeclaration(declaration);
            if (problem is null)
                validDeclarations.Add(declaration);
            else
                problems.Add(problem);
        }

        return new StageDeclarationResult(validDeclarations, problems);
    }

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

        if (!ImplementsStageContract(stageType))
            return new RequestFlowValidationProblem(
                ProblemCodes.StageMissingContract,
                $"'{stageType.FullName}' does not implement IRequestStage<TRequest, TResponse> or " +
                "IRequestStage<TRequest>; implement one of them or remove the AddStage call.",
                stageType);

        if (stageType.IsGenericTypeDefinition && !ClosesOverItsOwnParameters(stageType))
        {
            string parameterNames = string.Join(", ", GetParameterNames(stageType));

            return new RequestFlowValidationProblem(
                ProblemCodes.StageParametersMisused,
                $"'{stageType.FullName}' declares generic parameters <{parameterNames}> that its " +
                "IRequestStage implementation does not use as its request. An open generic stage " +
                "implements IRequestStage<TRequest, TResponse> with its own two parameters in that " +
                "order, or declares one parameter and uses it as the request: IRequestStage<TRequest> " +
                "for void requests, or IRequestStage<TRequest, TResponse> with a fixed response type.",
                stageType);
        }

        return null;
    }

    private static bool ImplementsStageContract(Type stageType)
    {
        foreach (var iface in stageType.GetInterfaces())
        {
            if (!iface.IsGenericType)
                continue;

            Type definition = iface.GetGenericTypeDefinition();
            if (definition == typeof(IRequestStage<,>) || definition == typeof(IRequestStage<>))
                return true;
        }

        return false;
    }

    // MakeGenericType substitutes positionally and StageClosing closes a one-parameter
    // definition over the request alone, so the interface's request argument has to be the
    // stage's own parameter for the closed type to name the dispatched request. A stage that
    // breaks this closes into a type no request can match.
    private static bool ClosesOverItsOwnParameters(Type stageType)
    {
        Type[] parameters = stageType.GetGenericArguments();

        foreach (var iface in stageType.GetInterfaces())
        {
            if (!iface.IsGenericType)
                continue;

            Type definition = iface.GetGenericTypeDefinition();
            if (definition != typeof(IRequestStage<,>) && definition != typeof(IRequestStage<>))
                continue;

            Type[] arguments = iface.GetGenericArguments();

            if (definition == typeof(IRequestStage<,>)
                && parameters.Length == 2
                && arguments[0] == parameters[0]
                && arguments[1] == parameters[1])
                return true;

            // One parameter naming the request: the void form, or the general form with the
            // response fixed by the class, as in Stage<TRequest> : IRequestStage<TRequest, Result>.
            if (parameters.Length == 1 && arguments[0] == parameters[0])
                return true;
        }

        return false;
    }

    private static string[] GetParameterNames(Type stageType)
    {
        Type[] parameters = stageType.GetGenericArguments();
        string[] names = new string[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
            names[i] = parameters[i].Name;

        return names;
    }
}

/// <summary>
/// Shape-valid declarations and shape problems produced by validating the recorded
/// declarations.
/// </summary>
internal sealed class DeclarationResult(
    IReadOnlyList<GenericHandlerDeclaration> validDeclarations, IReadOnlyList<RequestFlowValidationProblem> problems)
{
    public IReadOnlyList<GenericHandlerDeclaration> ValidDeclarations { get; } = validDeclarations;

    public IReadOnlyList<RequestFlowValidationProblem> Problems { get; } = problems;
}

/// <summary>
/// Closed handler types and constraint problems produced by validating the declared
/// closings.
/// </summary>
internal sealed class ClosingResult(IReadOnlyList<Type> closedTypes, IReadOnlyList<RequestFlowValidationProblem> problems)
{
    public IReadOnlyList<Type> ClosedTypes { get; } = closedTypes;

    public IReadOnlyList<RequestFlowValidationProblem> Problems { get; } = problems;
}

/// <summary>
/// The stage declarations that passed the shape check, plus one problem for each declaration
/// that failed.
/// </summary>
internal sealed class StageDeclarationResult(
    IReadOnlyList<StageDeclaration> validDeclarations, IReadOnlyList<RequestFlowValidationProblem> problems)
{
    public IReadOnlyList<StageDeclaration> ValidDeclarations { get; } = validDeclarations;

    public IReadOnlyList<RequestFlowValidationProblem> Problems { get; } = problems;
}
