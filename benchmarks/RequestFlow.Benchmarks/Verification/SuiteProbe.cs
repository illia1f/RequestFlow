using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.Harness;
using RequestFlow.Benchmarks.RequestFlowFixtures.ShortCircuit;

namespace RequestFlow.Benchmarks;

public static class SuiteProbe
{
    public static BenchmarkProbeResult Run()
    {
        ShortCircuitServices.VerifyRequestFlow(valueTask: false);
        ShortCircuitServices.VerifyRequestFlow(valueTask: true);

        var expectations = new Dictionary<Type, Func<IReadOnlyDictionary<string, object?>, BenchmarkExpectation>>
        {
            [typeof(ChainLevelBenchmarks)] = parameters => BenchmarkExpectation.Request(
                    stages: (int)parameters[nameof(ChainLevelBenchmarks.StageCount)]!) with
                {
                    VoidMethods = [nameof(ChainLevelBenchmarks.TaskVoidChain), nameof(ChainLevelBenchmarks.ValueVoidChain)],
                },
            [typeof(VoidCompletionBenchmarks)] = parameters => BenchmarkExpectation.VoidRequest(
                stages: (bool)parameters[nameof(VoidCompletionBenchmarks.WithPipeline)]! ? 1 : 0),
            [typeof(ShortCircuitRetainedScopeBenchmarks)] = ShortCircuitWorkload.Expectation,
            [typeof(ShortCircuitScopePerOperationBenchmarks)] = ShortCircuitWorkload.Expectation,
        };

        return BenchmarkVerification.Run(typeof(Program).Assembly, expectations);
    }
}
