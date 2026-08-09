namespace Orders.Api.Validation;

/// <summary>
/// A request that checks itself before its handler runs.
/// </summary>
public interface IValidatableRequest
{
    IEnumerable<string> Validate();
}
