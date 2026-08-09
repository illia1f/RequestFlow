using System;

namespace RequestFlow;

/// <summary>
/// One stage in a request's chain: the <c>AddStage</c> call it came from and the type that runs.
/// </summary>
/// <remarks>
/// A rule usually needs both, and for a stage registered already closed they are the same type.
/// Compare <see cref="ClosedType"/> to tell whether two entries are the same stage, since two
/// separate calls can land on one type. Name <see cref="DeclaredType"/> in the message, because
/// that is the <c>AddStage</c> call the reader has to go and change.
/// <para>
/// Same class does not mean same <see cref="ClosedType"/>. <c>in TRequest</c> lets a stage closed
/// over a base request cover the requests under it, so with <c>PlaceOrder : IAudited</c> both
/// <c>LoggingStage&lt;IAudited, OrderId&gt;</c> and <c>LoggingStage&lt;PlaceOrder, OrderId&gt;</c>
/// land in the <c>PlaceOrder</c> chain.
/// </para>
/// </remarks>
public sealed class ClosedStageModel
{
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    internal ClosedStageModel(Type declaredType, Type closedType, Type? contractType = null)
    {
        DeclaredType = declaredType ?? throw new ArgumentNullException(nameof(declaredType));
        ClosedType = closedType ?? throw new ArgumentNullException(nameof(closedType));
        ContractType = OpenContract.OrDefault(
            contractType, typeof(IRequestStage<,>), typeof(IRequestStage<>));
    }

    /// <summary>
    /// The type <c>AddStage</c> was given, open or closed.
    /// </summary>
    public Type DeclaredType { get; }

    /// <summary>
    /// The stage type built for this request, always closed.
    /// </summary>
    /// <remarks>
    /// The type the container resolves. A two-parameter stage over a void request closes over
    /// <see cref="NoResult"/> here, so read <see cref="HandlerModel.IsVoid"/> rather than these
    /// type arguments.
    /// </remarks>
    public Type ClosedType { get; }

    /// <summary>
    /// The open generic stage contract this closing satisfies.
    /// </summary>
    /// <remarks>
    /// Either <c>IRequestStage</c> or an interface built on one.
    /// </remarks>
    public Type ContractType { get; }
}
