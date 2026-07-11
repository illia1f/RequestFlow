using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Non-generic value for the <see cref="Type"/>-keyed dispatch map; the dispatcher
/// downcasts to <see cref="RequestPlan{TResponse}"/>.
/// </summary>
internal abstract class RequestPlanBase
{
    /// <summary>
    /// The response type this plan was registered with.
    /// </summary>
    public abstract Type ResponseType { get; }
}

/// <summary>
/// Plan typed over the response only, so the dispatcher can invoke it without
/// knowing the concrete request type.
/// </summary>
internal abstract class RequestPlan<TResponse> : RequestPlanBase
{
    /// <inheritdoc />
    public sealed override Type ResponseType => typeof(TResponse);

    /// <summary>
    /// Resolves the handler from <paramref name="services"/> and invokes it.
    /// </summary>
    public abstract Task<TResponse> ExecuteAsync(
        IRequest<TResponse> request, IServiceProvider services, CancellationToken cancellationToken);
}
