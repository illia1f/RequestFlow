using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Completes a void handler's or stage's task as a <see cref="NoResult"/> task, reusing the task
/// it was given where it can and the cached one where it cannot.
/// </summary>
internal static class NoResultBridge
{
    // A stage that hands back its next call's task hands back the level's own Task<NoResult>, so the
    // cast recovers it and a running pass-through level crosses free. An async stage's builder makes
    // a plain Task, which falls through to the wrap. The cast sits behind the completed check, which
    // is the cheaper test and the one a synchronous chain answers on.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<NoResult> Complete(Task task)
        => Succeeded(task) ? NoResult.Task : (task as Task<NoResult> ?? AwaitAsync(task));

    public static Task<NoResult> CompleteOrNull(Task? task)
        => task is null ? null! : Complete(task);

    private static bool Succeeded(Task task)
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
