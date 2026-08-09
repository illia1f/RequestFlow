using System.Diagnostics;
using RequestFlow;

namespace Orders.Api.Stages;

/// <summary>
/// Times every request in the application. Registered without a filter, so it closes over commands
/// and queries alike, void ones included.
/// </summary>
public sealed class LoggingStage<TRequest, TResponse>(ILogger<LoggingStage<TRequest, TResponse>> logger)
    : IRequestStage<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
    {
        long start = Stopwatch.GetTimestamp();
        try
        {
            return await next.InvokeAsync(cancellationToken);
        }
        finally
        {
            StageLog.Timed(logger, typeof(TRequest).Name, Stopwatch.GetElapsedTime(start));
        }
    }
}

/// <summary>
/// The stage's log messages, generated rather than formatted.
/// </summary>
/// <remarks>
/// A dispatch reaches this on every request, and the params overload of <c>LogInformation</c> would
/// build an array and box the elapsed time there whether or not the level is on.
/// </remarks>
internal static partial class StageLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "{Request} took {Elapsed}.")]
    public static partial void Timed(ILogger logger, string request, TimeSpan elapsed);
}
