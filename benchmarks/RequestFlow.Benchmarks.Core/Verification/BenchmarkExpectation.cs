namespace RequestFlow.Benchmarks.Core;

/// <summary>
/// The expected workload for one benchmark parameter set.
/// </summary>
/// <param name="ChildScopeMethod">The benchmark method whose library resolves handlers in a child scope per send.</param>
/// <param name="ChildScopes">How many child scopes one call of <paramref name="ChildScopeMethod"/> opens.</param>
/// <param name="VoidMethods">Benchmark methods that return no response, so <paramref name="Result"/> does not apply to them.</param>
public sealed record BenchmarkExpectation(
    string[] Names,
    int? Result = null,
    bool Ordered = true,
    int ParallelHandlers = 0,
    IReadOnlyDictionary<string, Type[]>? HandlerTypes = null,
    string? ChildScopeMethod = null,
    int ChildScopes = 1,
    IReadOnlyCollection<string>? VoidMethods = null)
{
    /// <summary>
    /// A request that returns the shared workload result.
    /// </summary>
    public static BenchmarkExpectation Request(int stages = 0, string prefix = TraceNames.StagePrefix)
        => new(BenchmarkVerification.RequestNames(stages, prefix), BenchmarkWorkload.Expected);

    /// <summary>
    /// A request without a response value.
    /// </summary>
    public static BenchmarkExpectation VoidRequest(int stages = 0, string prefix = TraceNames.StagePrefix)
        => new(BenchmarkVerification.RequestNames(stages, prefix));

    /// <summary>
    /// A fully consumed stream with its checksum.
    /// </summary>
    public static BenchmarkExpectation Stream(int stages, int count)
        => new(BenchmarkVerification.StreamNames(stages, count), count * (count - 1) / 2);

    /// <summary>
    /// An event with the declared handler ordering and fan-out gate.
    /// </summary>
    public static BenchmarkExpectation Event(int handlers, bool ordered = true, int parallelHandlers = 0)
        => new(BenchmarkVerification.EventNames(handlers), Ordered: ordered, ParallelHandlers: parallelHandlers);

    /// <summary>
    /// Checks the trace, result and declared handler types.
    /// </summary>
    public void Verify(BenchmarkObservation observation)
    {
        int? result = VoidMethods?.Contains(observation.Method) == true ? null : Result;

        BenchmarkVerification.VerifyWorkload(observation, Names, result, ordered: Ordered);

        if (HandlerTypes is null)
            return;

        if (!HandlerTypes.TryGetValue(observation.Method, out Type[]? expectedTypes))
            throw new InvalidOperationException($"Benchmark '{observation.BenchmarkType.Name}.{observation.Method}' has no handler type expectations.");

        BenchmarkVerification.VerifyHandlerTypes(observation, expectedTypes);
    }
}
