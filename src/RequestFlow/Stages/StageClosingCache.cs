// Startup only: nothing here runs on the dispatch path.

using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Remembers each <see cref="StageClosing"/> answer per declaration and handler pair; null
/// records "does not apply". Registration, the freeze, and the aliased-stage check all walk
/// the same cross product, so the registry owns one instance and each pair pays the
/// reflective closing once.
/// </summary>
internal sealed class StageClosingCache
{
    private readonly Dictionary<ClosingKey, Type?> _closings = [];

    /// <summary>
    /// <see cref="StageClosing"/>'s TryClose with the answer cached per pair.
    /// </summary>
    public bool TryClose(StageDeclaration declaration, HandlerRegistration handler, out Type closedStageType)
    {
        var key = new ClosingKey(declaration, handler);

        // Locked because every provider built from the collection freezes once, and two
        // providers can freeze at the same time. Registration and the freeze are the only
        // callers, so the lock never sits on the dispatch path.
        lock (_closings)
        {
            if (!_closings.TryGetValue(key, out Type? closed))
            {
                closed = StageClosing.TryClose(declaration, handler, out Type type) ? type : null;
                _closings[key] = closed;
            }

            closedStageType = closed!;
            return closed is not null;
        }
    }

    // Declarations and handlers accumulate once in the registry and every caller hands the
    // same instances back, so reference identity is the key. Spelled out because net462 has
    // no ValueTuple and the library takes no dependency to get one.
    private readonly struct ClosingKey(StageDeclaration declaration, HandlerRegistration handler)
        : IEquatable<ClosingKey>
    {
        private readonly StageDeclaration _declaration = declaration;
        private readonly HandlerRegistration _handler = handler;

        public bool Equals(ClosingKey other)
            => ReferenceEquals(_declaration, other._declaration) && ReferenceEquals(_handler, other._handler);

        public override bool Equals(object? obj)
            => obj is ClosingKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (_declaration.GetHashCode() * 397) ^ _handler.GetHashCode();
            }
        }
    }
}
