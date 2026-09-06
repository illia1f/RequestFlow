using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// The validation model and stage declaration facts captured from one registry.
/// </summary>
internal sealed class RegistrationSnapshot
{
    private RegistrationSnapshot(RequestFlowModel model, StageDeclarationFacts stageFacts)
    {
        Model = model;
        StageFacts = stageFacts;
    }

    public RequestFlowModel Model { get; }

    public StageDeclarationFacts StageFacts { get; }

    public static RegistrationSnapshot Capture(
        IReadOnlyList<HandlerRegistration> handlers,
        IReadOnlyList<Type> requestTypes,
        IReadOnlyList<StageDeclaration> stageDeclarations,
        StageClosingCache closings)
        => Capture(
            handlers,
            requestTypes,
            stageDeclarations,
            closings,
            EventClosure.Build([], []));

    public static RegistrationSnapshot Capture(
        IReadOnlyList<HandlerRegistration> handlers,
        IReadOnlyList<Type> requestTypes,
        IReadOnlyList<StageDeclaration> stageDeclarations,
        StageClosingCache closings,
        EventClosureResult eventClosure)
    {
        HashSet<StageDeclaration> reachedDeclarations = [];

        // Scanned requests first in scan order, then requests only a handler covers.
        List<Type> orderedRequests = [];
        HashSet<Type> seen = [];
        foreach (var requestType in requestTypes)
        {
            if (seen.Add(requestType))
                orderedRequests.Add(requestType);
        }

        foreach (var handler in handlers)
        {
            if (seen.Add(handler.RequestType))
                orderedRequests.Add(handler.RequestType);
        }

        Dictionary<Type, List<HandlerRegistration>> handlersByRequest = [];
        foreach (var handler in handlers)
        {
            if (!handlersByRequest.TryGetValue(handler.RequestType, out List<HandlerRegistration>? covering))
            {
                covering = [];
                handlersByRequest[handler.RequestType] = covering;
            }

            covering.Add(handler);
        }

        Dictionary<Type, Type> memoRequestContracts = [];
        Dictionary<Type, Type> memoValueContracts = [];
        Dictionary<Type, Type> memoStreamContracts = [];
        Dictionary<Type, Dictionary<Type, Type>> memoClosingContracts = [];

        // Reused across declarations: one declaration reaching two handlers as the same closed type
        // is one chain slot, not two.
        HashSet<Type> closedPerDeclaration = [];

        List<RequestModel> requests = [];
        foreach (var requestType in orderedRequests)
        {
            List<HandlerModel> requestHandlers = [];
            List<ClosedStageModel> chain = [];

            // Closing needs a handler registration; a request nothing handles gets no chain.
            if (handlersByRequest.TryGetValue(requestType, out List<HandlerRegistration>? covering))
            {
                foreach (var handler in covering)
                {
                    Type? responseType = handler.IsVoid ? null : handler.ResponseType;

                    requestHandlers.Add(new HandlerModel(
                        handler.ImplementationType,
                        responseType,
                        HandlerContract.Of(handler.ImplementationType, handler.Contract),
                        ModelLifetime.Of(handler.Lifetime)));
                }

                foreach (var declaration in stageDeclarations)
                {
                    closedPerDeclaration.Clear();
                    foreach (var handler in covering)
                    {
                        if (closings.TryClose(declaration, handler, out Type closedStageType)
                            && closedPerDeclaration.Add(closedStageType))
                        {
                            reachedDeclarations.Add(declaration);

                            chain.Add(new ClosedStageModel(
                                declaration.StageType,
                                closedStageType,
                                StageContract.OfClosing(
                                    closedStageType,
                                    declaration.Family,
                                    handler.RequestType,
                                    handler.ResponseType,
                                    handler.IsVoid,
                                    memoClosingContracts)));
                        }
                    }
                }
            }

            requests.Add(new RequestModel(requestType, [.. requestHandlers], [.. chain]));
        }

        // Reach is read off the chains, so the requests have to exist before the declarations do.
        RequestModel[] capturedRequests = [.. requests];

        StageDeclarationModel[] declaredStages = new StageDeclarationModel[stageDeclarations.Count];
        for (int i = 0; i < stageDeclarations.Count; i++)
        {
            StageDeclaration declaration = stageDeclarations[i];

            declaredStages[i] = new StageDeclarationModel(
                declaration.StageType,
                ModelLifetime.Of(declaration.Lifetime),
                StageReach.Of(capturedRequests, declaration.StageType),
                StageContract.Of(
                    declaration.StageType,
                    declaration.Family,
                    SelectMemo(
                        declaration.Family,
                        memoRequestContracts,
                        memoValueContracts,
                        memoStreamContracts)));
        }

        var model = new RequestFlowModel(
            capturedRequests,
            declaredStages,
            eventClosure.Events,
            eventClosure.EventSubscriptions,
            eventClosure.EventStrategies);
        return new RegistrationSnapshot(
            model, new StageDeclarationFacts(stageDeclarations, reachedDeclarations));
    }

    private static Dictionary<Type, Type> SelectMemo(
        StageFamily family,
        Dictionary<Type, Type> request,
        Dictionary<Type, Type> value,
        Dictionary<Type, Type> stream)
    {
        if (family == StageFamily.Stream)
            return stream;
        if (family == StageFamily.Value)
            return value;

        return request;
    }
}
