using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RequestFlow;

/// <summary>
/// One <c>AddStage</c> or <c>AddStreamStage</c> call, holding the stage type the application wrote.
/// </summary>
/// <remarks>
/// Read <see cref="ReachedRequests"/> for the requests the call landed on, and read the rest of the
/// entry for checks about the call itself, such as the same stage registered twice.
/// </remarks>
public sealed class StageDeclarationModel
{
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
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
    /// The requests this stage type reached, in request order. Empty when it reached none.
    /// </summary>
    /// <remarks>
    /// Derived from the chains the model holds, so it lists every request whose
    /// <see cref="RequestModel.Stages"/> contains a closing of this <see cref="StageType"/>.
    /// Two calls registering one stage type report the same requests, whatever each call
    /// filtered on. That registration already fails under <c>RF0103</c>.
    /// </remarks>
    public IReadOnlyList<Type> ReachedRequests { get; }

    /// <summary>
    /// The open generic stage contract this declaration implements.
    /// </summary>
    /// <remarks>
    /// A void stage carries <c>IRequestStage&lt;TRequest&gt;</c> and has to say so, since nothing
    /// in the declared type separates it from the typed form without reflection. A stage from
    /// another family, such as a stream stage, carries that family's contract.
    /// </remarks>
    public Type ContractType { get; }
}
