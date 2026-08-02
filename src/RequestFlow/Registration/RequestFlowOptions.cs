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

    /// <exception cref="ArgumentNullException"/>
    internal RequestFlowOptions Apply(Action<RequestFlowOptions> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        configure(this);

        return this;
    }

    /// <summary>
    /// Lifetime for discovered handlers. Transient by default.
    /// </summary>
    public ServiceLifetime HandlerLifetime { get; private set; } = ServiceLifetime.Transient;

    /// <summary>
    /// Lifetime for <c>IRequestDispatcher</c>. Scoped by default.
    /// </summary>
    public ServiceLifetime DispatcherLifetime { get; private set; } = ServiceLifetime.Scoped;

    /// <summary>
    /// Registers this call's handlers with <paramref name="lifetime"/>.
    /// </summary>
    public RequestFlowOptions WithHandlerLifetime(ServiceLifetime lifetime)
    {
        HandlerLifetime = lifetime;
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
    /// to a chain once, so a second call naming it is a duplicate whatever it filters on. An
    /// invalid stage surfaces as a <see cref="RequestFlowValidationException"/> problem when
    /// the dispatch map is built.
    /// </remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="InvalidOperationException"/>
    public RequestFlowOptions AddStage(Type stageType, Action<StageApplicability>? configure = null)
    {
        if (stageType is null)
            throw new ArgumentNullException(nameof(stageType));

        var applicability = new StageApplicability();
        configure?.Invoke(applicability);

        StageDeclarations.Add(new StageDeclaration(stageType, applicability.HandlerFilter));

        return this;
    }

    /// <summary>
    /// Registers <typeparamref name="TStage"/> under the same rules as <see cref="AddStage(Type, Action{StageApplicability})"/>.
    /// </summary>
    public RequestFlowOptions AddStage<TStage>(Action<StageApplicability>? configure = null)
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
