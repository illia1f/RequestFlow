using System;

namespace RequestFlow;

/// <summary>
/// Builds one plan's chain of levels, bottom-up from the handler.
/// </summary>
internal static class ChainBuilder
{
    public static LevelEntry<TResponse> Typed<TRequest, TResponse>(StageChain chain)
        where TRequest : IRequest<TResponse>
    {
        LevelEntry<TResponse> level = TypedHandler<TRequest, TResponse>();

        for (int i = chain.StageTypes.Length - 1; i >= 0; i--)
            level = LevelFactory.Stage<TRequest, TResponse>(chain.StageTypes[i], level);

        return level;
    }

    /// <summary>
    /// The chain for a void request, whose stages come in both contract shapes. Which shape each
    /// level runs is read from <see cref="StageChain.TypedShapes"/>.
    /// </summary>
    public static LevelEntry<NoResult> Void<TRequest>(StageChain chain)
        where TRequest : IRequest<NoResult>
    {
        LevelEntry<NoResult> level = VoidHandler<TRequest>();

        for (int i = chain.StageTypes.Length - 1; i >= 0; i--)
            level = VoidStage<TRequest>(chain.StageTypes[i], chain.TypedShapes[i], level);

        return level;
    }

    public static LevelEntry<TResponse> TypedHandler<TRequest, TResponse>()
        where TRequest : IRequest<TResponse>
        => LevelFactory.Handler<TRequest, TResponse>();

    public static LevelEntry<NoResult> VoidHandler<TRequest>()
        where TRequest : IRequest<NoResult>
        => LevelFactory.VoidHandler<TRequest>();

    // A stage that implements both shapes runs as the two-parameter one, which the freeze recorded.
    private static LevelEntry<NoResult> VoidStage<TRequest>(
        Type stageType, bool typedShape, LevelEntry<NoResult> below)
        where TRequest : IRequest<NoResult>
        => typedShape
            ? LevelFactory.Stage<TRequest, NoResult>(stageType, below)
            : LevelFactory.VoidStage<TRequest>(stageType, below);
}
