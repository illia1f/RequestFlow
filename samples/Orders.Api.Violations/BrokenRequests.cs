using RequestFlow;
using RequestFlow.Cqrs;

namespace Orders.Api.Violations;

// These types produce the documented startup failures. They live in their own assembly so a normal
// start does not register them.

/// <summary>
/// Trips ORDERS0001: a query handler covers it, so the name has to end in "Query".
/// </summary>
public sealed record FetchOrderDetails(Guid Id) : IQuery<string>;

internal sealed class FetchOrderDetailsHandler : IQueryHandler<FetchOrderDetails, string>
{
    public Task<string> HandleAsync(FetchOrderDetails query, CancellationToken cancellationToken)
        => Task.FromResult($"order {query.Id}");
}

/// <summary>
/// Trips ORDERS0002: the handler leaves off IOrdersCommandHandler, so the chain runs without the validation stage.
/// </summary>
public sealed record ArchiveOrderCommand(Guid Id) : ICommand<Guid>;

internal sealed class ArchiveOrderCommandHandler : ICommandHandler<ArchiveOrderCommand, Guid>
{
    public Task<Guid> HandleAsync(ArchiveOrderCommand command, CancellationToken cancellationToken)
        => Task.FromResult(command.Id);
}

/// <summary>
/// Trips CQRS0001, which AddCqrs contributes, and RF0102, since nothing handles it.
/// </summary>
public sealed record RefundOrderCommand(Guid Id) : ICommand<Guid>, IQuery<Guid>;

/// <summary>
/// Trips RF0114: the scan finds the event, but nothing subscribes to it.
/// </summary>
public sealed record OrderRefunded(Guid Id) : IEvent;
