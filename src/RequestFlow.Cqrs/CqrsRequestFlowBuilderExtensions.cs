using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RequestFlow;
using RequestFlow.Cqrs;

// Same namespace convention as AddRequestFlow: AddCqrs needs no extra using.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// RequestFlow CQRS registration entry point.
/// </summary>
public static class CqrsRequestFlowBuilderExtensions
{
    /// <summary>
    /// Registers the typed dispatchers <see cref="ICommandDispatcher"/>,
    /// <see cref="IQueryDispatcher"/>, and <see cref="IStreamQueryDispatcher"/>. Command, query,
    /// and stream query handlers need no extra registration; the <c>AddRequestFlow</c> assembly
    /// scan discovers them.
    /// <para>
    /// Also adds a singleton validation rule to the startup pass. A request classified as both a
    /// command and a query, or as both a command and a stream query, fails the freeze
    /// with <c>CQRS0001</c>.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    public static RequestFlowBuilder AddCqrs(this RequestFlowBuilder builder)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        builder.Services.TryAddTransient<ICommandDispatcher, CqrsDispatcher>();
        builder.Services.TryAddTransient<IQueryDispatcher, CqrsDispatcher>();
        builder.Services.TryAddTransient<IStreamQueryDispatcher, CqrsStreamDispatcher>();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IRequestFlowValidationRule, CommandQuerySplitRule>());

        return builder;
    }
}
