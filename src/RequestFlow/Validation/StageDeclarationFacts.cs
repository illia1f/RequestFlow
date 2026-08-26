using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// What a stage declaration carries that <see cref="StageDeclarationModel"/> does not: the family
/// of the call that registered it, and the handler contract <c>WhereHandlerImplements</c> narrowed it to.
/// </summary>
/// <remarks>
/// A built-in rule reads the same public model a registered rule does, and that model records a
/// stage's contract rather than the call. Inferring the family back from the contract picks stream
/// first, so a stage type implementing a contract from both families would answer for the wrong
/// call. The freeze builds this from the declarations themselves and hands it to the rules that would otherwise guess.
/// <para>
/// One stage type registered twice already fails <c>RF0103</c>, so the first call answers for it.
/// <see cref="None"/> stands for a model built by hand, where every answer falls back to the contract.
/// </para>
/// </remarks>
internal sealed class StageDeclarationFacts
{
    public static readonly StageDeclarationFacts None = new();

    private readonly Dictionary<Type, StageFamily> _families = [];
    private readonly Dictionary<Type, Type> _handlerFilters = [];
    private readonly HashSet<Type> _mixedFamilies = [];

    public StageDeclarationFacts(IReadOnlyList<StageDeclaration> declarations)
    {
        foreach (var declaration in declarations)
        {
            if (_families.TryGetValue(declaration.StageType, out StageFamily? first))
            {
                if (first != declaration.Family)
                    _mixedFamilies.Add(declaration.StageType);

                continue;
            }

            _families.Add(declaration.StageType, declaration.Family);

            if (declaration.HandlerFilter is not null)
                _handlerFilters.Add(declaration.StageType, declaration.HandlerFilter);
        }
    }

    private StageDeclarationFacts()
    { }

    /// <summary>
    /// The family the call belongs to, falling back to what <paramref name="contractType"/> implies.
    /// </summary>
    public StageFamily GetFamily(Type stageType, Type contractType)
        => _families.TryGetValue(stageType, out StageFamily? family)
            ? family
            : StageFamily.FromContract(contractType);

    /// <summary>
    /// The handler contract the call was narrowed to, or null when it named none.
    /// </summary>
    public Type? GetHandlerFilter(Type stageType)
        => _handlerFilters.TryGetValue(stageType, out Type? filter) ? filter : null;

    /// <summary>
    /// True when calls from both families registered this stage type.
    /// </summary>
    public bool RegisteredInBothFamilies(Type stageType)
        => _mixedFamilies.Contains(stageType);
}
