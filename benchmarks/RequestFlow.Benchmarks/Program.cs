using RequestFlow.Benchmarks.Harness;

namespace RequestFlow.Benchmarks;

public static class Program
{
    public static int Main(string[] args)
        => SuiteRunner.Run(typeof(Program).Assembly, "requestflow", args, SuiteProbe.Run);
}
