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
        => Succeeded(task) ? NoResult.Task : AwaitAsync(task);

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
