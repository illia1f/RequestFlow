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

        // Locked because two providers built from the same collection can freeze at once.
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

    // Every caller hands back the same declaration and handler instances the registry holds, so reference identity is the key.
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
