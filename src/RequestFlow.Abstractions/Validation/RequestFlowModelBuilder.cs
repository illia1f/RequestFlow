using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Builds a <see cref="RequestFlowModel"/> by hand, which is how a validation rule is unit
/// tested without a container.
/// </summary>
/// <remarks>
/// A partial model is allowed on purpose. A rule that never reads handlers is tested against
/// requests that have none, so the builder does not require shapes the freeze would always produce.
/// </remarks>
public sealed class RequestFlowModelBuilder
{
    private readonly Dictionary<Type, RequestModelBuilder> _requests = [];
    private readonly List<Type> _requestOrder = [];
    private readonly List<StageDeclarationInput> _stageDeclarations = [];

    /// <summary>
    /// Adds a request type, or configures one already added. Repeated calls for one type
    /// configure a single entry, the way the freeze groups handlers by request type.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    public RequestFlowModelBuilder AddRequest(Type requestType, Action<RequestModelBuilder>? configure = null)
    {
        if (requestType is null)
            throw new ArgumentNullException(nameof(requestType));

        if (!_requests.TryGetValue(requestType, out RequestModelBuilder? request))
        {
            request = new RequestModelBuilder();
            _requests[requestType] = request;
            _requestOrder.Add(requestType);
        }

        configure?.Invoke(request);

        return this;
    }

    /// <summary>
    /// Adds one stage declaration, as a single <c>AddStage</c> call would.
    /// </summary>
    /// <remarks>
    /// Leaving <paramref name="contractType"/> null records <c>IRequestStage&lt;TRequest, TResponse&gt;</c>, matching
    /// <see cref="RequestModelBuilder.AddStage"/>. Naming one takes an open generic interface,
    /// since that is what a rule compares against.
    /// </remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    public RequestFlowModelBuilder AddStageDeclaration(Type stageType, Type? contractType = null)
        => AddStageDeclaration(stageType, RequestFlowLifetime.Transient, contractType);

    /// <summary>
    /// Adds one stage declaration registered with the named lifetime.
    /// </summary>
    /// <remarks>
    /// The overload without a lifetime records <see cref="RequestFlowLifetime.Transient"/>, which is
    /// what <c>AddStage</c> uses unless the application asks for something else.
    /// </remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    public RequestFlowModelBuilder AddStageDeclaration(
        Type stageType, RequestFlowLifetime lifetime, Type? contractType = null)
    {
        if (stageType is null)
            throw new ArgumentNullException(nameof(stageType));

        // Resolve now, so a bad contract throws from the call that named it rather than from Build.
        Type resolved = OpenContract.OrDefault(
            contractType, typeof(IRequestStage<,>), typeof(IRequestStage<>));

        _stageDeclarations.Add(new StageDeclarationInput(stageType, lifetime, resolved));

        return this;
    }

    /// <summary>
    /// Produces the model, with requests in the order they were first added.
    /// </summary>
    /// <remarks>
    /// Callable more than once, with each call producing an independent model.
    /// </remarks>
    public RequestFlowModel Build()
    {
        RequestModel[] requests = new RequestModel[_requestOrder.Count];
        for (int i = 0; i < requests.Length; i++)
        {
            Type requestType = _requestOrder[i];
            requests[i] = _requests[requestType].Build(requestType);
        }

        StageDeclarationModel[] declarations = new StageDeclarationModel[_stageDeclarations.Count];
        for (int i = 0; i < declarations.Length; i++)
        {
            StageDeclarationInput input = _stageDeclarations[i];
            declarations[i] = new StageDeclarationModel(
                input.StageType, input.Lifetime, StageReach.Of(requests, input.StageType),
                input.ContractType);
        }

        return new RequestFlowModel(requests, declarations);
    }

    /// <summary>
    /// Produces the context a rule is given at startup, wrapping a freshly built model.
    /// </summary>
    /// <remarks>
    /// The flags default to what registration does unless the application opts out: a request with
    /// no handler is a problem, a stage that reached nothing is not.
    /// </remarks>
    public RequestFlowValidationContext BuildContext(
        bool unhandledRequestsAllowed = false, bool unusedStagesDisallowed = false)
        => new(Build(), unhandledRequestsAllowed, unusedStagesDisallowed);

    private readonly struct StageDeclarationInput(
        Type stageType, RequestFlowLifetime lifetime, Type contractType)
    {
        public Type StageType { get; } = stageType;

        public RequestFlowLifetime Lifetime { get; } = lifetime;

        public Type ContractType { get; } = contractType;
    }
}
