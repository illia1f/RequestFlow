using System;

namespace RequestFlow;

/// <summary>
/// Thrown when an event publish strategy returns a null task from <c>PublishAsync</c>.
/// </summary>
public sealed class EventStrategyNullTaskException : NullTaskException
{
    /// <exception cref="ArgumentNullException"/>
    public EventStrategyNullTaskException(Type strategyType)
        : base(BuildMessage(strategyType))
    {
        StrategyType = strategyType;
    }

    /// <summary>
    /// The type of the strategy that returned the null task.
    /// </summary>
    public Type StrategyType { get; }

    private static string BuildMessage(Type strategyType)
    {
        if (strategyType is null)
            throw new ArgumentNullException(nameof(strategyType));

        return $"The event publish strategy '{strategyType.FullName}' returned a null task from PublishAsync.";
    }
}
