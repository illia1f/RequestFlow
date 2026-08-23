using System;
using System.Collections.Generic;

namespace RequestFlow;

internal enum MessageContract
{
    Request,
    StreamRequest,
    Event,
}

/// <summary>
/// Reports a message type that belongs to two incompatible contract families.
/// </summary>
internal sealed class ContractConflictRule(
    MessageContract first,
    MessageContract second,
    string problemCode) : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(
        RequestFlowValidationContext context)
    {
        HashSet<Type> visited = [];

        foreach (var request in context.Model.Requests)
        {
            if (visited.Add(request.RequestType)
                && HasContract(request.RequestType, first)
                && HasContract(request.RequestType, second))
            {
                yield return Problem(request.RequestType);
            }
        }

        foreach (var @event in context.Model.Events)
        {
            if (visited.Add(@event.EventType)
                && HasContract(@event.EventType, first)
                && HasContract(@event.EventType, second))
            {
                yield return Problem(@event.EventType);
            }
        }
    }

    private RequestFlowValidationProblem Problem(Type messageType)
        => new(problemCode, MessageFor(messageType), messageType);

    private string MessageFor(Type messageType)
        => first == MessageContract.Request && second == MessageContract.StreamRequest
            ? RequestAndStreamRequestMessage(messageType)
            : SharedMessage(messageType);

    // RF0109 names what the conflict costs the dispatch map, which the shared wording cannot.
    private static string RequestAndStreamRequestMessage(Type messageType)
        => $"Request '{messageType.FullName}' implements both IRequest and IStreamRequest; " +
            "the map holds one plan per request type, so one would overwrite the other. Keep " +
            "one contract and split the type if both are needed.";

    private string SharedMessage(Type messageType)
        => $"Type '{messageType.FullName}' implements both {Name(first)} and {Name(second)}; " +
            "a message type must use one RequestFlow contract family. Keep one contract and split " +
            "the type if both roles are needed.";

    private static bool HasContract(Type messageType, MessageContract contract)
        => contract switch
        {
            MessageContract.Request => Implements(messageType, typeof(IRequest<>)),
            MessageContract.StreamRequest => Implements(messageType, typeof(IStreamRequest<>)),
            MessageContract.Event => typeof(IEvent).IsAssignableFrom(messageType),
            _ => throw new ArgumentOutOfRangeException(nameof(contract)),
        };

    private static bool Implements(Type messageType, Type contractDefinition)
    {
        foreach (var contract in messageType.GetInterfaces())
        {
            if (contract.IsGenericType
                && contract.GetGenericTypeDefinition() == contractDefinition)
            {
                return true;
            }
        }

        return false;
    }

    private static string Name(MessageContract contract)
        => contract switch
        {
            MessageContract.Request => "IRequest",
            MessageContract.StreamRequest => "IStreamRequest",
            MessageContract.Event => "IEvent",
            _ => throw new ArgumentOutOfRangeException(nameof(contract)),
        };
}
