using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RequestFlow;

/// <summary>
/// One <c>AddStage</c>, <c>AddValueStage</c>, or <c>AddStreamStage</c> registration.
/// </summary>
public sealed class StageDeclarationModel
{
    internal StageDeclarationModel(
        Type stageType, RequestFlowLifetime lifetime, Type[] reachedRequests, Type? contractType = null)
    {
        StageType = stageType ?? throw new ArgumentNullException(nameof(stageType));
        Lifetime = lifetime;
        ReachedRequests = new ReadOnlyCollection<Type>(
            reachedRequests ?? throw new ArgumentNullException(nameof(reachedRequests)));
        ContractType = OpenContract.OrDefault(
            contractType, typeof(IRequestStage<,>), typeof(IRequestStage<>));
    }

    /// <summary>
    /// The type the registering call was given: an open generic definition or a closed class.
    /// </summary>
    public Type StageType { get; }

    /// <summary>
    /// The lifetime the stage is registered with.
    /// </summary>
    public RequestFlowLifetime Lifetime { get; }

    /// <summary>
    /// The requests this stage type reached, in request order.
    /// </summary>
    /// <remarks>
    /// Derived from <see cref="RequestModel.Stages"/>.
    /// Duplicate registrations report the same requests regardless of their filters; <c>RF0103</c> reports the duplicate.
    /// </remarks>
    public IReadOnlyList<Type> ReachedRequests { get; }

    /// <summary>
    /// The open generic stage contract this declaration implements.
    /// </summary>
    public Type ContractType { get; }
}
