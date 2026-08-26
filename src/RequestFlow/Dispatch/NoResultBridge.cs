using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Completes a void handler's or stage's task as a <see cref="NoResult"/> task, reusing the task
/// it was given where it can and the cached one where it cannot.
/// </summary>
internal static class NoResultBridge
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<NoResult> Complete(Task task)
        => IsCompletedSuccessfully(task) ? NoResult.Task : (task as Task<NoResult> ?? AwaitAsync(task));

    public static Task<NoResult> CompleteOrNull(Task? task)
        => task is null ? null! : Complete(task);

    private static bool IsCompletedSuccessfully(Task task)
#if NET8_0_OR_GREATER
        => task.IsCompletedSuccessfully;
#else
        => task.Status == TaskStatus.RanToCompletion;
#endif

    private static async Task<NoResult> AwaitAsync(Task task)
    {
        await task.ConfigureAwait(false);
        return NoResult.Value;
    }
}
