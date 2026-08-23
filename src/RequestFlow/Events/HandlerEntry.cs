using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

internal delegate Task HandlerEntry(
    IServiceProvider services,
    object @event,
    CancellationToken cancellationToken);
