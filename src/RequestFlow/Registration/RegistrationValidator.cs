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
    /// Checks each stage declaration's shape, stopping at that declaration's first failure.
    /// Duplicate stage types need every declaration at once, so a separate check covers them.
    /// </summary>
    public static StageDeclarationResult ValidateStageDeclarations(IReadOnlyList<StageDeclaration> declarations)
    {
        List<StageDeclaration> validDeclarations = [];
        List<string> problems = [];

        foreach (var declaration in declarations)
        {
            string? problem = ValidateStageDeclaration(declaration);
            if (problem is null)
                validDeclarations.Add(declaration);
            else
                problems.Add(problem);
        }

        return new StageDeclarationResult(validDeclarations, problems);
    }

    private static string? ValidateStageDeclaration(StageDeclaration declaration)
    {
        Type stageType = declaration.StageType;

        if (stageType.IsInterface)
            return $"'{stageType.FullName}' is an interface; only concrete stage classes can be registered.";

        if (stageType.IsAbstract)
            return $"'{stageType.FullName}' is abstract; only concrete stage classes can be registered.";

        if (!stageType.IsGenericTypeDefinition && stageType.ContainsGenericParameters)
            return $"'{stageType.FullName}' is partially closed; register either the open generic definition " +
                   "or a fully closed stage type.";

        if (!ImplementsStageContract(stageType))
            return $"'{stageType.FullName}' does not implement IRequestStage<TRequest, TResponse> or " +
                   "IRequestStage<TRequest>; implement one of them or remove the AddStage call.";

        if (stageType.IsGenericTypeDefinition && !ClosesOverItsOwnParameters(stageType))
        {
            string parameterNames = string.Join(", ", GetParameterNames(stageType));

            return $"'{stageType.FullName}' declares generic parameters <{parameterNames}> that its " +
                   "IRequestStage implementation does not use as its request. An open generic stage " +
                   "implements IRequestStage<TRequest, TResponse> with its own two parameters in that " +
                   "order, or declares one parameter and uses it as the request: IRequestStage<TRequest> " +
                   "for void requests, or IRequestStage<TRequest, TResponse> with a fixed response type.";
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
    // breaks this closes into a type no request can match, or drags along a parameter its contract never uses.
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

    /// <summary>
    /// Reports every stage type registered more than once. Called once at freeze, so a stage
    /// added by two separate <c>AddRequestFlow</c> calls is caught. The handler filter is not
    /// part of the key: one stage type belongs to a chain once, whatever the calls filtered on.
    /// </summary>
    public static List<string> ValidateDuplicateStages(IReadOnlyList<StageDeclaration> declarations)
    {
        List<string> problems = [];

        HashSet<Type> seenStages = [];
        foreach (var declaration in declarations)
        {
            if (!seenStages.Add(declaration.StageType))
                problems.Add(
                    $"Stage '{declaration.StageType.FullName}' from assembly " +
                    $"'{declaration.StageType.Assembly.GetName().Name}' is registered more than once and would run " +
                    "twice in the same chain; remove the duplicate AddStage call. A handler filter does not make " +
                    "a second registration distinct.");
        }

        return problems;
    }

    /// <summary>
    /// Reports two different stage declarations that reach one request as the same stage
    /// class: an open definition next to its own closed form, or two closed forms that both
    /// apply through the request's base type. Runs at freeze, where every request is known;
    /// one stage type registered twice is <see cref="ValidateDuplicateStages"/>'s job.
    /// </summary>
    public static List<string> ValidateAliasedStages(
        IReadOnlyList<StageDeclaration> declarations,
        IReadOnlyList<HandlerRegistration> handlers,
        StageClosingCache closings)
    {
        List<string> problems = [];

        // One message per colliding pair of declarations, not per request they collide on.
        HashSet<StagePair> reported = [];

        // Keyed on the stage class rather than the closed type, because in TRequest lets two
        // different closings of one class apply to the same request.
        Dictionary<Type, StageOwner> owners = [];

        foreach (var handler in handlers)
        {
            owners.Clear();
            foreach (var declaration in declarations)
            {
                if (!closings.TryClose(declaration, handler, out Type closedStageType))
                    continue;

                Type stageClass = closedStageType.IsGenericType
                    ? closedStageType.GetGenericTypeDefinition()
                    : closedStageType;

                if (!owners.TryGetValue(stageClass, out StageOwner owner))
                {
                    owners[stageClass] = new StageOwner(declaration, closedStageType);
                    continue;
                }

                if (owner.Declaration.StageType == declaration.StageType)
                    continue;

                if (!reported.Add(new StagePair(owner.Declaration.StageType, declaration.StageType)))
                    continue;

                problems.Add(owner.ClosedStageType == closedStageType
                    ? $"Stages '{owner.Declaration.StageType.FullName}' and '{declaration.StageType.FullName}' both " +
                      $"resolve to '{closedStageType.FullName}' for request " +
                      $"'{handler.RequestType.FullName}' and would run twice in the same chain; remove " +
                      "one of the two AddStage calls."
                    : $"Stages '{owner.Declaration.StageType.FullName}' and '{declaration.StageType.FullName}' are " +
                      $"the same stage class and both apply to request '{handler.RequestType.FullName}'; the class " +
                      "would run twice in the same chain; remove one of the two AddStage calls.");
            }
        }

        return problems;
    }

    /// <summary>
    /// Reports every stage that reached no request. Runs at freeze, and only when the
    /// application called <c>DisallowUnusedStages</c>.
    /// </summary>
    public static List<string> ValidateUnusedStages(
        IReadOnlyList<StageDeclaration> declarations, ISet<Type> appliedStageTypes)
    {
        List<string> problems = [];

        foreach (var declaration in declarations)
        {
            if (!appliedStageTypes.Contains(declaration.StageType))
                problems.Add(
                    $"Stage '{declaration.StageType.FullName}' from assembly " +
                    $"'{declaration.StageType.Assembly.GetName().Name}' applies to no registered request; widen its " +
                    "generic constraints, scan the assembly holding the requests it targets, or drop " +
                    "DisallowUnusedStages.");
        }

        return problems;
    }

    // The first declaration seen for a stage class under one handler, with the closed type it
    // produced, so the collision message can say whether the pair met on one closed type or on
    // two closings of the class. Spelled out because net462 has no ValueTuple and the library
    // takes no dependency to get one.
    private readonly struct StageOwner(StageDeclaration declaration, Type closedStageType)
    {
        public StageDeclaration Declaration { get; } = declaration;

        public Type ClosedStageType { get; } = closedStageType;
    }

    // Two stage types reported together once.
    private readonly struct StagePair(Type first, Type second) : IEquatable<StagePair>
    {
        private readonly Type _first = first;
        private readonly Type _second = second;

        public bool Equals(StagePair other)
            => _first == other._first && _second == other._second;

        public override bool Equals(object? obj)
            => obj is StagePair other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (_first.GetHashCode() * 397) ^ _second.GetHashCode();
            }
        }
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

/// <summary>
/// The stage declarations that passed the shape check, plus one problem message for each
/// declaration that failed.
/// </summary>
internal sealed class StageDeclarationResult(
    IReadOnlyList<StageDeclaration> validDeclarations, IReadOnlyList<string> problems)
{
    public IReadOnlyList<StageDeclaration> ValidDeclarations { get; } = validDeclarations;

    public IReadOnlyList<string> Problems { get; } = problems;
}
