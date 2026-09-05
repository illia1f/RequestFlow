using System.Threading.Tasks;

namespace RequestFlow;

internal static class ValueNoResultBridge
{
    public static ValueTask<NoResult> Complete(ValueTask task)
    {
        if (task.IsCompletedSuccessfully)
        {
            task.GetAwaiter().GetResult();
            return new ValueTask<NoResult>(NoResult.Value);
        }

        return AwaitComplete(task);
    }

    public static ValueTask Discard(ValueTask<NoResult> task)
    {
        if (task.IsCompletedSuccessfully)
        {
            task.GetAwaiter().GetResult();
            return default;
        }

        return AwaitDiscard(task);
    }

    private static async ValueTask<NoResult> AwaitComplete(ValueTask task)
    {
        await task.ConfigureAwait(false);
        return NoResult.Value;
    }

    private static async ValueTask AwaitDiscard(ValueTask<NoResult> task)
    {
        await task.ConfigureAwait(false);
    }
}
