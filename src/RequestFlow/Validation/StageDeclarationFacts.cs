using System;
using System.Collections.Generic;
using System.Text;

namespace RequestFlow;

/// <summary>
/// Stage registration families, filters, and reach that the public model combines by stage type.
/// </summary>
/// <remarks>
/// Preserves the first registered family for diagnostics.
/// <see cref="None"/> uses the model's contracts when no registration facts are available.
/// </remarks>
internal sealed class StageDeclarationFacts
{
    public static readonly StageDeclarationFacts None = new([]);

    private readonly Dictionary<Type, List<StageDeclaration>> _declarations = [];
    private readonly Dictionary<Type, List<StageFamily>> _families = [];
    private readonly Dictionary<Type, HashSet<StageFamily>>? _reachedFamilies;

    public StageDeclarationFacts(
        IReadOnlyList<StageDeclaration> declarations,
        IReadOnlyCollection<StageDeclaration>? reachedDeclarations = null)
    {
        foreach (var declaration in declarations)
        {
            if (!_declarations.TryGetValue(
                declaration.StageType,
                out List<StageDeclaration>? sameType))
            {
                sameType = [];
                _declarations.Add(declaration.StageType, sameType);
            }

            sameType.Add(declaration);

            if (_families.TryGetValue(
                declaration.StageType,
                out List<StageFamily>? families))
            {
                bool seen = false;
                foreach (StageFamily family in families)
                {
                    if (family == declaration.Family)
                    {
                        seen = true;
                        break;
                    }
                }

                if (!seen)
                    families.Add(declaration.Family);

                continue;
            }

            _families.Add(declaration.StageType, [declaration.Family]);
        }

        if (reachedDeclarations is null)
            return;

        Dictionary<Type, HashSet<StageFamily>> reachedFamilies = [];
        foreach (StageDeclaration declaration in reachedDeclarations)
        {
            if (!reachedFamilies.TryGetValue(declaration.StageType, out HashSet<StageFamily>? families))
            {
                families = [];
                reachedFamilies.Add(declaration.StageType, families);
            }

            families.Add(declaration.Family);
        }

        _reachedFamilies = reachedFamilies;
    }

    /// <summary>
    /// The first registered family, falling back to what <paramref name="contractType"/> implies.
    /// </summary>
    public StageFamily GetFamily(Type stageType, Type contractType)
        => _families.TryGetValue(stageType, out List<StageFamily>? families)
            ? families[0]
            : StageFamily.FromContract(contractType);

    public bool HasFamily(Type stageType, StageFamily family, Type contractType)
    {
        if (!_families.TryGetValue(stageType, out List<StageFamily>? families))
            return StageFamily.FromContract(contractType) == family;

        foreach (StageFamily declaredFamily in families)
        {
            if (declaredFamily == family)
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when a declaration in <paramref name="family"/> is unfiltered or admits a handler for <paramref name="request"/>.
    /// </summary>
    public bool AnyDeclarationAdmits(
        Type stageType,
        StageFamily family,
        RequestModel request)
    {
        if (!_declarations.TryGetValue(stageType, out List<StageDeclaration>? declarations))
            return true;

        foreach (StageDeclaration declaration in declarations)
        {
            if (declaration.Family != family)
                continue;

            Type? handlerFilter = declaration.HandlerFilter;
            if (handlerFilter is null)
                return true;

            foreach (HandlerModel handler in request.Handlers)
            {
                if (handlerFilter.IsAssignableFrom(handler.HandlerType))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when any declaration in <paramref name="family"/> produced a closing.
    /// Uses <paramref name="modelFallback"/> only when reach information is unavailable.
    /// </summary>
    public bool AnyDeclarationReached(
        Type stageType,
        StageFamily family,
        bool modelFallback)
    {
        if (_reachedFamilies is null)
            return modelFallback;

        return _reachedFamilies.TryGetValue(stageType, out HashSet<StageFamily>? families)
            && families.Contains(family);
    }

    /// <summary>
    /// Builds the duplicate-removal advice from the distinct registration calls in call order.
    /// </summary>
    public string BuildDuplicateRemovalAdvice(Type stageType, Type contractType)
    {
        IReadOnlyList<StageFamily> families = GetFamilies(stageType, contractType);
        if (families.Count == 1)
            return $"Remove the duplicate {families[0].CallName} call.";

        return $"Keep one registration and remove the other calls ({FormatCallNames(families)}).";
    }

    private IReadOnlyList<StageFamily> GetFamilies(Type stageType, Type contractType)
        => _families.TryGetValue(stageType, out List<StageFamily>? families)
            ? families
            : [StageFamily.FromContract(contractType)];

    private static string FormatCallNames(IReadOnlyList<StageFamily> families)
    {
        var names = new StringBuilder();
        for (int i = 0; i < families.Count; i++)
        {
            if (i > 0)
            {
                if (i == families.Count - 1)
                    names.Append(families.Count == 2 ? " and " : ", and ");
                else
                    names.Append(", ");
            }

            names.Append(families[i].CallName);
        }

        return names.ToString();
    }
}
