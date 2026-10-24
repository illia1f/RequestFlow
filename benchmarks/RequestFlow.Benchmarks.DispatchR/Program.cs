using RequestFlow.Benchmarks.Harness;

namespace RequestFlow.Benchmarks.DispatchR;

public static class Program
{
    public static int Main(string[] args)
        => SuiteRunner.Run(typeof(Program).Assembly, "dispatchr", args,
            SuiteProbe.Run, SuiteProbe.ReportLifetimes);
}
