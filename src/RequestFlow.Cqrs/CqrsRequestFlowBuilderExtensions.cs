using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RequestFlow;
using RequestFlow.Cqrs;

// Microsoft's own convention for registration extensions: AddCqrs is visible in Program.cs without an extra using.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// RequestFlow CQRS registration entry point.
/// </summary>
public static class CqrsRequestFlowBuilderExtensions
{
    /// <summary>
    /// Registers the typed dispatchers <see cref="ICommandDispatcher"/> and
    /// <see cref="IQueryDispatcher"/>. Command and query handlers need no extra
    /// registration; the <c>AddRequestFlow</c> assembly scan discovers them.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    public static RequestFlowBuilder AddCqrs(this RequestFlowBuilder builder)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        builder.Services.TryAddTransient<ICommandDispatcher, CqrsDispatcher>();
        builder.Services.TryAddTransient<IQueryDispatcher, CqrsDispatcher>();

        return builder;
    }
}
