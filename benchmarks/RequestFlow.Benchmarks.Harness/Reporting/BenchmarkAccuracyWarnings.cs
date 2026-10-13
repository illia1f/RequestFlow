namespace RequestFlow.Benchmarks.Harness;

internal static class BenchmarkAccuracyWarnings
{
    internal static string? Describe(string benchmark, double mean, double confidenceIntervalHalfWidth)
    {
        if (!double.IsFinite(mean) || mean <= 0 || !double.IsFinite(confidenceIntervalHalfWidth))
        {
            return FormattableString.Invariant(
                       $"{benchmark}: cannot calculate relative timing error from mean = {mean:G6} ns and 99.9% confidence interval half-width = {confidenceIntervalHalfWidth:G6} ns. ") +
                   "The mean must be finite and greater than zero, and the half-width must be finite. " +
                   "Inspect BenchmarkDotNet's validation messages and raw measurements to determine why these statistics are invalid. " +
                   "This row and ratios involving it cannot support timing claims.";
        }

        double relativeError = confidenceIntervalHalfWidth / mean;

        if (relativeError <= BenchmarkConfig.MaximumRelativeError)
            return null;

        return FormattableString.Invariant(
                   $"{benchmark}: mean = {mean:G6} ns; 99.9% confidence interval half-width = {confidenceIntervalHalfWidth:G6} ns. ") +
               FormattableString.Invariant(
                   $"Relative error (half-width / mean) is {relativeError * 100:G6}%, above the {BenchmarkConfig.MaximumRelativeError * 100:G6}% target. ") +
               "This measures uncertainty in the estimated mean, not the range of individual operation times. " +
               "Inspect raw measurements for timing drift within each launch and disagreement between launches; these summary statistics do not identify the cause. " +
               "Keep this row and ratios involving it out of timing claims until measurement precision is established.";
    }
}
