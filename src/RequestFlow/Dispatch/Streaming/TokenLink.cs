using System.Threading;

namespace RequestFlow;

/// <summary>
/// Joins the token a dispatch was given with the one its enumeration was given.
/// </summary>
internal static class TokenLink
{
    /// <summary>
    /// Sets <paramref name="linked"/> to a token that is cancelled when either input is, and returns
    /// the source behind it for the caller to dispose. Returns null when the two are equal or at most
    /// one can be cancelled, since that token can be used as it is and no source is worth allocating.
    /// </summary>
    public static CancellationTokenSource? Combine(
        CancellationToken first, CancellationToken second, out CancellationToken linked)
    {
        if (!second.CanBeCanceled || first == second)
        {
            linked = first;
            return null;
        }

        if (!first.CanBeCanceled)
        {
            linked = second;
            return null;
        }

        CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(first, second);
        linked = source.Token;

        return source;
    }
}
