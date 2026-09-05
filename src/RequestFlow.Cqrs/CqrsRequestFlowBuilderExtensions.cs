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
    /// Registers command, query, and stream query dispatchers and the CQRS validation rule.
    /// <c>AddRequestFlow</c> discovers their handlers.
    /// Requests classified as both commands and queries fail validation with <c>CQRS0001</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    public static RequestFlowBuilder AddCqrs(this RequestFlowBuilder builder)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        builder.Services.TryAddTransient<ICommandDispatcher, CqrsDispatcher>();
        builder.Services.TryAddTransient<IQueryDispatcher, CqrsDispatcher>();
        builder.Services.TryAddTransient<IValueCommandDispatcher, CqrsValueDispatcher>();
        builder.Services.TryAddTransient<IValueQueryDispatcher, CqrsValueDispatcher>();
        builder.Services.TryAddTransient<IStreamQueryDispatcher, CqrsStreamDispatcher>();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IRequestFlowValidationRule, CommandQuerySplitRule>());

        return builder;
    }
}
