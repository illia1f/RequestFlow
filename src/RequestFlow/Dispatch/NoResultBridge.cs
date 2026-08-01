using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Completes a void handler's task as a <see cref="NoResult"/> task, reusing the cached
/// task when the handler finished synchronously.
/// </summary>
internal static class NoResultBridge
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<NoResult> Complete(Task task)
        => task.Status == TaskStatus.RanToCompletion ? NoResult.Task : AwaitAsync(task);

    /// <summary>
    /// <see cref="Complete"/> for callers with a null-task guard downstream: a null task
    /// passes through unchanged so the guard can name the stage or handler that returned it.
    /// </summary>
    public static Task<NoResult> CompleteOrNull(Task? task)
        => task is null ? null! : Complete(task);

    private static async Task<NoResult> AwaitAsync(Task task)
    {
        await task.ConfigureAwait(false);
        return NoResult.Value;
    }
}
