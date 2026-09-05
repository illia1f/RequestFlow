using System;

namespace RequestFlow;

/// <summary>
/// One stage in a request's chain, with its declared and closed types.
/// </summary>
/// <remarks>
/// Compare <see cref="ClosedType"/> to identify duplicate stages.
/// Use <see cref="DeclaredType"/> in diagnostics to identify the registration to change.
/// Contravariance can put different closed forms of one stage class in the same chain.
/// </remarks>
public sealed class ClosedStageModel
{
    internal ClosedStageModel(Type declaredType, Type closedType, Type? contractType = null)
    {
        DeclaredType = declaredType ?? throw new ArgumentNullException(nameof(declaredType));
        ClosedType = closedType ?? throw new ArgumentNullException(nameof(closedType));
        ContractType = OpenContract.OrDefault(
            contractType, typeof(IRequestStage<,>), typeof(IRequestStage<>));
    }

    /// <summary>
    /// The type the registering call was given, open or closed.
    /// </summary>
    public Type DeclaredType { get; }

    /// <summary>
    /// The stage type built for this request, always closed.
    /// </summary>
    /// <remarks>
    /// Use <see cref="HandlerModel.IsVoid"/> to identify void handlers.
    /// </remarks>
    public Type ClosedType { get; }

    /// <summary>
    /// The open generic stage contract this closing satisfies.
    /// </summary>
    public Type ContractType { get; }
}
