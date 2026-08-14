namespace RequestFlow;

/// <summary>
/// Marks a message that is dispatched to exactly one handler and yields a sequence of
/// <typeparamref name="TItem"/>.
/// </summary>
/// <typeparam name="TItem">The type each element of the sequence has.</typeparam>
public interface IStreamRequest<out TItem>
{ }
