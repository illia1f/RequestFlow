using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// Everything user-configurable about RequestFlow, passed to the <c>AddRequestFlow</c>
/// configure delegate.
/// </summary>
public sealed class RequestFlowOptions
{
    internal List<Assembly> Assemblies { get; } = [];

    internal List<GenericHandlerDeclaration> Declarations { get; } = [];

    internal List<StageDeclaration> StageDeclarations { get; } = [];

    internal bool UnusedStagesDisallowed { get; private set; }

    internal ServiceLifetime HandlerLifetime { get; private set; } = ServiceLifetime.Transient;

    internal ServiceLifetime DispatcherLifetime { get; private set; } = ServiceLifetime.Scoped;

    /// <exception cref="ArgumentNullException"/>
    internal RequestFlowOptions Apply(Action<RequestFlowOptions> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        configure(this);

        return this;
    }

    /// <summary>
    /// Registers this call's handlers with a scoped lifetime instead of the default transient.
    /// Applies only to the handlers this call discovers; a later call decides for its own.
    /// Transient and scoped are the whole set: a singleton handler pins every dependency it
    /// injects for the life of the process.
    /// </summary>
    public RequestFlowOptions WithScopedHandlers()
    {
        HandlerLifetime = ServiceLifetime.Scoped;
        return this;
    }

    internal bool UnhandledRequestsAllowed { get; private set; }

    /// <summary>
    /// Skips the missing-handler check when the dispatch map freezes. Intended for
    /// contracts assemblies whose requests are handled elsewhere. Applies to all
    /// registered assemblies once any call opts in; duplicate-handler validation is
    /// unaffected.
    /// </summary>
    public RequestFlowOptions AllowUnhandledRequests()
    {
        UnhandledRequestsAllowed = true;
        return this;
    }

    /// <summary>
    /// Registers the dispatcher with a transient lifetime instead of the default scoped.
    /// The first <c>AddRequestFlow</c> call fixes the dispatcher lifetime; later calls
    /// cannot change it.
    /// </summary>
    public RequestFlowOptions WithTransientDispatcher()
    {
        DispatcherLifetime = ServiceLifetime.Transient;
        return this;
    }

    /// <summary>
    /// Scans the assembly containing <typeparamref name="T"/> for handlers and requests.
    /// </summary>
    public RequestFlowOptions RegisterHandlersFromAssemblyContaining<T>()
        => RegisterHandlersFromAssembly(typeof(T).Assembly);

    /// <summary>
    /// Scans <paramref name="assembly"/> for handlers and requests.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    public RequestFlowOptions RegisterHandlersFromAssembly(Assembly assembly)
    {
        if (assembly is null)
            throw new ArgumentNullException(nameof(assembly));

        if (!Assemblies.Contains(assembly))
            Assemblies.Add(assembly);

        return this;
    }

    /// <summary>
    /// Registers <paramref name="handlerType"/>, an open generic handler definition with one
    /// type parameter, closed over each type in <paramref name="closingTypes"/>. The scan
    /// ignores open generic handlers; every closing must be declared here. Only null
    /// arguments throw at the call; an invalid declaration surfaces as a
    /// <see cref="RequestFlowValidationException"/> problem when the dispatch map is built.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    public RequestFlowOptions RegisterGenericHandler(Type handlerType, params Type[] closingTypes)
    {
        if (handlerType is null)
            throw new ArgumentNullException(nameof(handlerType));
        if (closingTypes is null)
            throw new ArgumentNullException(nameof(closingTypes));

        foreach (var closingType in closingTypes)
        {
            if (closingType is null)
                throw new ArgumentException("Closing types must not contain null.", nameof(closingTypes));
        }

        Declarations.Add(new GenericHandlerDeclaration(handlerType, closingTypes));

        return this;
    }

    /// <summary>
    /// Registers <paramref name="stageType"/> to run around the handler of every request it
    /// applies to. Registration order is execution order, outermost first.
    /// </summary>
    /// <remarks>
    /// Pass an open generic definition such as <c>typeof(LoggingStage&lt;,&gt;)</c> to let the
    /// stage's own constraints decide which requests it reaches, or a closed stage class to
    /// target one request contract. A closed stage is not restricted to the request type it
    /// names: <c>TRequest</c> is contravariant, so it also wraps every request deriving from
    /// that one, and <paramref name="configure"/> narrows the set further. A stage type belongs
    /// to a chain once, so a second call naming it is a duplicate whatever it filters on. Each
    /// stage carries its own lifetime, transient unless <paramref name="configure"/> says
    /// otherwise. An invalid stage surfaces as a <see cref="RequestFlowValidationException"/>
    /// problem when the dispatch map is built.
    /// </remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="InvalidOperationException"/>
    public RequestFlowOptions AddStage(Type stageType, Action<StageOptions>? configure = null)
    {
        if (stageType is null)
            throw new ArgumentNullException(nameof(stageType));

        var stage = new StageOptions();
        configure?.Invoke(stage);

        StageDeclarations.Add(new StageDeclaration(stageType, stage.HandlerFilter, stage.Lifetime));

        return this;
    }

    /// <summary>
    /// Registers <typeparamref name="TStage"/> under the same rules as <see cref="AddStage(Type, Action{StageOptions})"/>.
    /// </summary>
    public RequestFlowOptions AddStage<TStage>(Action<StageOptions>? configure = null)
        where TStage : class
        => AddStage(typeof(TStage), configure);

    /// <summary>
    /// Reports a stage that reaches no registered request as a validation problem instead of
    /// leaving it a silent no-op. Applies to all registered stages once any call opts in.
    /// </summary>
    public RequestFlowOptions DisallowUnusedStages()
    {
        UnusedStagesDisallowed = true;
        return this;
    }
}
