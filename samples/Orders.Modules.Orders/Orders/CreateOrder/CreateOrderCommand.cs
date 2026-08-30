using Orders.Modules.Orders.Validation;
using RequestFlow.Cqrs;

namespace Orders.Modules.Orders;

public sealed record CreateOrderCommand(string Customer, decimal Total) : ICommand<Guid>, IValidatableRequest
{
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Customer))
            yield return "Customer is required.";

        if (Total <= 0)
            yield return "Total has to be greater than zero.";
    }
}
