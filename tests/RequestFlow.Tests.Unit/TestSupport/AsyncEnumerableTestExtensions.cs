namespace RequestFlow.Tests.Unit;

internal static class AsyncEnumerableTestExtensions
{
    internal static async Task<List<T>> CollectAsync<T>(this IAsyncEnumerable<T> stream)
    {
        List<T> items = [];
        await foreach (T item in stream)
        {
            items.Add(item);
        }

        return items;
    }
}
