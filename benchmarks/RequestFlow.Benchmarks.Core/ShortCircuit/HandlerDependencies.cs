namespace RequestFlow.Benchmarks.Core;

public sealed class CommandBuffer(DependencyState state) : DependencyComponent(state, "command-buffer")
{
    public byte[] Bytes { get; } = state.Settings.BufferBytes == 0
        ? Array.Empty<byte>()
        : new byte[state.Settings.BufferBytes];
}

public sealed class DataSession(DependencyState state, CommandBuffer buffer)
    : DependencyComponent(state, "data-session")
{
    public int Capacity => buffer.Bytes.Length;
}

public sealed class Repository(DependencyState state, DataSession session) : DependencyComponent(state, "repository")
{
    public Task<int> Read()
        => session.Capacity >= 0
            ? ShortCircuitWorkload.Handled
            : throw new InvalidOperationException();

    public ValueTask<int> ReadValue()
        => session.Capacity >= 0
            ? new(ShortCircuitWorkload.HandledResult)
            : throw new InvalidOperationException();
}
