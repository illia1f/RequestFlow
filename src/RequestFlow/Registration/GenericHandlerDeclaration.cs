using System;

namespace RequestFlow;

/// <summary>
/// One recorded <c>RegisterGenericHandler</c> call: the handler definition plus every
/// type argument declared to close it over.
/// </summary>
internal sealed class GenericHandlerDeclaration(Type handlerType, Type[] closingTypes)
{
    /// <summary>
    /// The open generic handler definition, e.g. <c>typeof(AuditHandler&lt;&gt;)</c>.
    /// </summary>
    public Type HandlerType { get; } = handlerType;

    /// <summary>
    /// The type arguments to close the handler over.
    /// </summary>
    public Type[] ClosingTypes { get; } = closingTypes;
}
