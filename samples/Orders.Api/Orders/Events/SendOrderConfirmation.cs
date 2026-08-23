using RequestFlow;

namespace Orders.Api.Orders;

/// <summary>
/// Stands in for the email a real application sends when an order is placed.
/// </summary>
public sealed class SendOrderConfirmation(ILogger<SendOrderConfirmation> logger)
    : IEventHandler<OrderPlaced>
{
    public Task HandleAsync(OrderPlaced placed, CancellationToken cancellationToken)
    {
        ConfirmationLog.Sent(logger, placed.OrderId, placed.Customer);
        return Task.CompletedTask;
    }
}

internal static partial class ConfirmationLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Confirmation for order {OrderId} sent to {Customer}.")]
    public static partial void Sent(ILogger logger, Guid orderId, string customer);
}
