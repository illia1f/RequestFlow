namespace RequestFlow;

internal static class ValueChainBuilder
{
    public static ValueLevelEntry<TResponse> Typed<TRequest, TResponse>(StageChain chain)
        where TRequest : IValueRequest<TResponse>
    {
        ValueLevelEntry<TResponse> level = TypedHandler<TRequest, TResponse>();

        for (int i = chain.StageTypes.Length - 1; i >= 0; i--)
            level = ValueLevelFactory.Stage<TRequest, TResponse>(chain.StageTypes[i], level);

        return level;
    }

    public static ValueVoidLevelEntry Void<TRequest>(StageChain chain)
        where TRequest : IValueRequest<NoResult>
    {
        ValueVoidLevelEntry level = VoidHandler<TRequest>();

        for (int i = chain.StageTypes.Length - 1; i >= 0; i--)
        {
            level = chain.TypedShapes[i]
                ? ValueLevelFactory.TypedVoidStage<TRequest>(chain.StageTypes[i], level)
                : ValueLevelFactory.VoidStage<TRequest>(chain.StageTypes[i], level);
        }

        return level;
    }

    public static ValueLevelEntry<TResponse> TypedHandler<TRequest, TResponse>()
        where TRequest : IValueRequest<TResponse>
        => ValueLevelFactory.Handler<TRequest, TResponse>();

    public static ValueVoidLevelEntry VoidHandler<TRequest>()
        where TRequest : IValueRequest<NoResult>
        => ValueLevelFactory.VoidHandler<TRequest>();
}
