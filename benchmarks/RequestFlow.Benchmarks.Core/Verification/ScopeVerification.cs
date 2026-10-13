using System.Reflection;
using static RequestFlow.Benchmarks.Core.TraceAssertions;

namespace RequestFlow.Benchmarks.Core;

internal static class ScopeVerification
{
    public static ScopeLifetime For(Type benchmarkType)
        => benchmarkType.GetCustomAttribute<BenchmarkScopeAttribute>()?.Lifetime
            ?? throw new InvalidOperationException($"Benchmark '{benchmarkType.Name}' has no [BenchmarkScope] attribute.");

    public static void VerifyAfterCall(BenchmarkObservation observation, BenchmarkExpectation expectation)
    {
        ScopeLifetime lifetime = For(observation.BenchmarkType);

        if (lifetime == ScopeLifetime.DirectHandler)
            return;

        IReadOnlyList<ExecutionRecord> records = observation.Trace ?? [];
        string label = BenchmarkVerification.Label(observation);
        bool childScopes = expectation.ChildScopeMethod == observation.Method;

        Require(records.Count > 0, label + " recorded no scoped components.");

        if (lifetime == ScopeLifetime.RetainedScope)
        {
            Require(!childScopes, label + " runs handlers in a child scope per send, so a retained scope cannot retain them.");
            Require(Witnesses(records).Length == 1, label + " did not run every component in its retained scope.");
            Require(records.All(record => !record.Scope.IsDisposed && !record.Component.IsDisposed),
                label + " disposed a component of its retained scope.");

            return;
        }

        int scopes = childScopes ? expectation.ChildScopes : 1;

        Require(Witnesses(records).Length == scopes, $"{label} ran its components in {Witnesses(records).Length} scopes, expected {scopes}.");
        Require(records.All(record => record.Scope.IsDisposed && record.Component.IsDisposed),
            label + " completed before disposing its operation scope.");
    }

    public static void VerifyAcrossCalls(BenchmarkObservation first, BenchmarkObservation second, BenchmarkExpectation expectation)
    {
        ScopeLifetime lifetime = For(first.BenchmarkType);

        if (lifetime == ScopeLifetime.DirectHandler)
            return;

        IReadOnlyList<ExecutionRecord> firstRecords = first.Trace ?? [];
        IReadOnlyList<ExecutionRecord> secondRecords = second.Trace ?? [];
        string label = BenchmarkVerification.Label(first);

        if (lifetime == ScopeLifetime.RetainedScope)
        {
            Require(Witnesses(firstRecords).SequenceEqual(Witnesses(secondRecords)), label + " changed its retained scope between calls.");
            Require(Components(firstRecords).SetEquals(Components(secondRecords)), label + " changed a scoped component between calls.");

            return;
        }

        Require(!Witnesses(firstRecords).Intersect(Witnesses(secondRecords)).Any(), label + " reused an operation scope.");
        Require(!Components(firstRecords).Overlaps(Components(secondRecords)), label + " reused a scoped component across operations.");
    }

    private static ScopeWitness[] Witnesses(IReadOnlyList<ExecutionRecord> records)
        => records.Select(record => record.Scope).Distinct(ReferenceEqualityComparer.Instance).Cast<ScopeWitness>().ToArray();

    private static HashSet<object> Components(IReadOnlyList<ExecutionRecord> records)
        => new(records.Select(record => (object)record.Component), ReferenceEqualityComparer.Instance);
}
