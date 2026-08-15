namespace RequestFlow.Cqrs;

/// <summary>
/// Marks a request that reads state without mutating it and yields a sequence of <typeparamref name="TItem"/>.
/// </summary>
/// <typeparam name="TItem">The type each element of the sequence has.</typeparam>
public interface IStreamQuery<out TItem> : IStreamRequest<TItem>
{ }
