using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// The marker a guard slot holds while a next call is being claimed.
/// </summary>
internal static class GuardSentinel
{
    /// <summary>
    /// Never completes, so a level that reads it treats the claiming call as still in flight.
    /// </summary>
    public static readonly Task Claimed = new TaskCompletionSource<object>().Task;
}
