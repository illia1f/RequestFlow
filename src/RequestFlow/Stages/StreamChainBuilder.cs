namespace RequestFlow;

/// <summary>
/// Builds one stream plan's chain of levels, bottom-up from the handler.
/// </summary>
internal static class StreamChainBuilder
{
    public static StreamLevelEntry<TItem> Build<TRequest, TItem>(StageChain chain)
        where TRequest : IStreamRequest<TItem>
    {
        StreamLevelEntry<TItem> level = Handler<TRequest, TItem>();

        for (int i = chain.StageTypes.Length - 1; i >= 0; i--)
            level = StreamLevelFactory.Stage<TRequest, TItem>(chain.StageTypes[i], level);

        return level;
    }

    public static StreamLevelEntry<TItem> Handler<TRequest, TItem>()
        where TRequest : IStreamRequest<TItem>
        => StreamLevelFactory.Handler<TRequest, TItem>();
}
