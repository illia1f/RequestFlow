namespace RequestFlow.Cqrs;

/// <summary>
/// Produces the sequence for a single stream query type.
/// </summary>
/// <typeparam name="TQuery">The stream query handled.</typeparam>
/// <typeparam name="TItem">The type each element of the sequence has.</typeparam>
public interface IStreamQueryHandler<in TQuery, TItem> : IStreamRequestHandler<TQuery, TItem>
    where TQuery : IStreamQuery<TItem>
{ }
