using RequestFlow.Benchmarks.Harness;

namespace RequestFlow.Benchmarks.MediatR;

public static class Program
{
    public static int Main(string[] args)
        => SuiteRunner.Run(typeof(Program).Assembly, "mediatr", args,
            SuiteProbe.Run, SuiteProbe.ReportLifetimes);
}
