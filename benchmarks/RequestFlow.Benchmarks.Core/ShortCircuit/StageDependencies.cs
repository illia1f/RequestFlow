namespace RequestFlow.Benchmarks.Core;

public sealed class AccessPolicy(DependencyState state) : DependencyComponent(state, "access-policy")
{
    public bool Allows(ShortCircuitMessage message) => message.Outcome != ShortCircuitOutcome.AuthorizationReject;
}

public sealed class CachePolicy(DependencyState state) : DependencyComponent(state, "cache-policy")
{
    public bool Contains(ShortCircuitMessage message) => message.Outcome == ShortCircuitOutcome.CacheHit;
}

public sealed class ChangeBuffer(DependencyState state) : DependencyComponent(state, "change-buffer")
{
    public byte[] Bytes { get; } = state.Settings.BufferBytes == 0
        ? Array.Empty<byte>()
        : new byte[state.Settings.BufferBytes];
}

public sealed class UnitOfWork(DependencyState state, ChangeBuffer buffer) : DependencyComponent(state, "unit-of-work")
{
    public int Capacity => buffer.Bytes.Length;
}
