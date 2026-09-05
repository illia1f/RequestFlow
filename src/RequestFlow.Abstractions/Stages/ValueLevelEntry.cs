using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

internal delegate ValueTask<TResponse> ValueLevelEntry<TResponse>(
    object request,
    IServiceProvider services,
    CancellationToken cancellationToken);

internal delegate ValueTask ValueVoidLevelEntry(
    object request,
    IServiceProvider services,
    CancellationToken cancellationToken);
