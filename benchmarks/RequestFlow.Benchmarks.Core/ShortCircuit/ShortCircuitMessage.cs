namespace RequestFlow.Benchmarks.Core;

public enum ShortCircuitOutcome
{
    AuthorizationReject,
    CacheHit,
    FullExecution
}

public class ShortCircuitMessage(ShortCircuitOutcome outcome) : WorkloadMessage
{
    public ShortCircuitOutcome Outcome { get; } = outcome;
}
