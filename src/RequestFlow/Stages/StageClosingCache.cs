using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Remembers each declaration and handler pair's closed type or exclusion reason.
/// </summary>
internal sealed class StageClosingCache
{
    private readonly Dictionary<ClosingKey, StageClosingResult> _closings = [];

    /// <summary>
    /// <see cref="StageClosing"/>'s TryClose with the answer cached per pair.
    /// </summary>
    public bool TryClose(StageDeclaration declaration, HandlerRegistration handler, out Type closedStageType)
    {
        StageClosingResult result = GetResult(declaration, handler);
        closedStageType = result.ClosedType!;
        return result.ClosedType is not null;
    }

    public StageClosingResult GetResult(StageDeclaration declaration, HandlerRegistration handler)
    {
        var key = new ClosingKey(declaration, handler);

        // Locked because two providers built from the same collection can freeze at once.
        lock (_closings)
        {
            if (!_closings.TryGetValue(key, out StageClosingResult result))
            {
                result = StageClosing.Match(declaration, handler);
                _closings[key] = result;
            }

            return result;
        }
    }

    // Callers pass the declaration and handler instances the registry holds, so reference identity
    // is enough for the key.
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
