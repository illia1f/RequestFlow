using RequestFlow;
using RequestFlow.Cqrs;

namespace Orders.Api.Violations;

// Everything here breaks a convention the Orders.Api rules enforce. The scan finds request types as
// well as handlers, so these need an assembly of their own that only --break-rules registers.

/// <summary>
/// Trips ORDERS0001: a query handler covers it, so the name has to end in "Query".
/// </summary>
public sealed record FetchOrderDetails(Guid Id) : IQuery<string>;

public sealed class FetchOrderDetailsHandler : IQueryHandler<FetchOrderDetails, string>
{
    public Task<string> HandleAsync(FetchOrderDetails query, CancellationToken cancellationToken)
        => Task.FromResult($"order {query.Id}");
}

/// <summary>
/// Trips ORDERS0002: the handler leaves off IOrdersCommandHandler, so the chain runs without the validation stage.
/// </summary>
public sealed record ArchiveOrderCommand(Guid Id) : ICommand<Guid>;

public sealed class ArchiveOrderCommandHandler : ICommandHandler<ArchiveOrderCommand, Guid>
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
