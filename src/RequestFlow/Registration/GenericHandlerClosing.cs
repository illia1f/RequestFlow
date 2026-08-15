using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// One declared closing of an open generic handler: the handler definition plus the type
/// argument to close it over.
/// </summary>
internal sealed record GenericHandlerClosing
{
    private GenericHandlerClosing(Type handlerType, Type closingType)
    {
        HandlerType = handlerType;
        ClosingType = closingType;
    }

    /// <summary>
    /// The open generic handler definition, e.g. <c>typeof(AuditHandler&lt;&gt;)</c>.
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// The type argument to close the handler over.
    /// </summary>
    public Type ClosingType { get; }

    /// <summary>
    /// Expands shape-valid declarations into one closing per declared closing type.
    /// </summary>
    public static IEnumerable<GenericHandlerClosing> Expand(
        IReadOnlyList<GenericHandlerDeclaration> declarations)
    {
        foreach (var declaration in declarations)
        {
            foreach (var closingType in declaration.ClosingTypes)
                yield return new GenericHandlerClosing(declaration.HandlerType, closingType);
        }
    }
}
