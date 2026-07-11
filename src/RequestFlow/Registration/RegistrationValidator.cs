using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Detects registration problems and is the only producer of problem strings.
/// </summary>
internal static class RegistrationValidator
{
    /// <summary>
    /// Shape checks per declaration, stopping at that declaration's first failure.
    /// </summary>
    public static DeclarationResult ValidateDeclarations(IReadOnlyList<GenericHandlerDeclaration> declarations)
    {
        List<GenericHandlerDeclaration> validDeclarations = [];
        List<string> problems = [];

        foreach (var declaration in declarations)
        {
            string? problem = ValidateDeclaration(declaration);
            if (problem is null)
                validDeclarations.Add(declaration);
            else
                problems.Add(problem);
        }

        return new DeclarationResult(validDeclarations, problems);
    }

    private static string? ValidateDeclaration(GenericHandlerDeclaration declaration)
    {
        Type handlerType = declaration.HandlerType;

        if (!handlerType.IsGenericTypeDefinition)
            return $"'{handlerType.FullName}' is not an open generic type definition; pass e.g. typeof(AuditHandler<>).";

        if (handlerType.IsAbstract)
            return $"'{handlerType.FullName}' is abstract; only concrete handler classes can be registered.";

        if (handlerType.GetGenericArguments().Length != 1)
            return $"'{handlerType.FullName}' has {handlerType.GetGenericArguments().Length} generic parameters; only single-parameter generic handlers are supported.";

        if (!ImplementsHandlerContract(handlerType))
            return $"'{handlerType.FullName}' does not implement IRequestHandler.";

        if (declaration.ClosingTypes.Length == 0)
            return $"Generic handler '{handlerType.FullName}' declares no closing types; at least one is required.";

        foreach (var closingType in declaration.ClosingTypes)
        {
            if (closingType.ContainsGenericParameters)
                return $"Closing type '{closingType.FullName}' for generic handler '{handlerType.FullName}' is not a closed type.";
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
        List<string> problems = [];

        foreach (var closing in closings)
        {
            try
            {
                closedTypes.Add(closing.HandlerType.MakeGenericType(closing.ClosingType));
            }
            catch (ArgumentException)
            {
                problems.Add(
                    $"Generic handler '{closing.HandlerType.FullName}' cannot be closed over " +
                    $"'{closing.ClosingType.FullName}'; the type argument violates the handler's generic constraints.");
            }
        }

        return new ClosingResult(closedTypes, problems);
    }

    /// <summary>
    /// Reports every request type covered by more than one handler. Called once at freeze.
    /// </summary>
    public static List<string> ValidateDuplicateHandlers(IReadOnlyList<HandlerRegistration> handlers)
    {
        List<string> problems = [];

        HashSet<Type> handledRequests = [];
        foreach (var handler in handlers)
        {
            if (!handledRequests.Add(handler.RequestType))
                problems.Add($"Request '{handler.RequestType.FullName}' has more than one handler; exactly one is required.");
        }

        return problems;
    }

    /// <summary>
    /// Reports every scanned request type that no handler covers. Called once at freeze.
    /// </summary>
    public static List<string> ValidateUnhandledRequests(
        IReadOnlyList<HandlerRegistration> handlers,
        IReadOnlyList<Type> requestTypes)
    {
        List<string> problems = [];

        HashSet<Type> handledRequests = [];
        foreach (var handler in handlers)
            handledRequests.Add(handler.RequestType);

        foreach (var requestType in requestTypes)
        {
            if (!handledRequests.Contains(requestType))
                problems.Add($"Request '{requestType.FullName}' has no handler.");
        }

        return problems;
    }
}

/// <summary>
/// Shape-valid declarations and shape problems produced by validating the recorded
/// declarations.
/// </summary>
internal sealed class DeclarationResult(
    IReadOnlyList<GenericHandlerDeclaration> validDeclarations, IReadOnlyList<string> problems)
{
    public IReadOnlyList<GenericHandlerDeclaration> ValidDeclarations { get; } = validDeclarations;

    public IReadOnlyList<string> Problems { get; } = problems;
}

/// <summary>
/// Closed handler types and constraint problems produced by validating the declared
/// closings.
/// </summary>
internal sealed class ClosingResult(IReadOnlyList<Type> closedTypes, IReadOnlyList<string> problems)
{
    public IReadOnlyList<Type> ClosedTypes { get; } = closedTypes;

    public IReadOnlyList<string> Problems { get; } = problems;
}
