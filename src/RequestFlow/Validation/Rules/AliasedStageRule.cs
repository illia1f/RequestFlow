using System;
using System.Collections.Generic;
using System.Text;

namespace RequestFlow;

/// <summary>
/// Reports declarations that put the same stage class into one request chain, including different closed generic forms.
/// One problem lists every colliding declaration.
/// </summary>
internal sealed class AliasedStageRule(StageDeclarationFacts? facts = null) : IRequestFlowValidationRule
{
    private readonly StageDeclarationFacts _facts = facts ?? StageDeclarationFacts.None;

    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        // One message per colliding set of declarations, not per request they collide on.
        HashSet<DeclarationSet> reported = [];

        // Keyed on the stage class where one handler makes the chain, because in TRequest lets
        // two different closings of one class apply to the same request. Several handlers mean
        // several chains merged into one list, so the key drops to the closed type there.
        Dictionary<Type, List<ClosedStageModel>> groups = [];

        // Keeps the groups in chain order, which is declaration order.
        List<Type> groupOrder = [];

        // Counted apart from the members, which drop verbatim duplicates, because a duplicate
        // still occupies a slot in the chain.
        Dictionary<Type, int> chainRuns = [];

        foreach (var request in context.Model.Requests)
        {
            groups.Clear();
            groupOrder.Clear();
            chainRuns.Clear();

            bool oneChain = request.Handlers.Count <= 1;

            foreach (var closing in request.Stages)
            {
                Type key = oneChain ? GetStageClass(closing.ClosedType) : closing.ClosedType;

                if (!groups.TryGetValue(key, out List<ClosedStageModel>? members))
                {
                    members = [];
                    groups[key] = members;
                    groupOrder.Add(key);
                }

                chainRuns[key] = chainRuns.TryGetValue(key, out int runs) ? runs + 1 : 1;

                // A declaration repeated verbatim is DuplicateStageRule's finding, not an alias.
                if (!ContainsDeclaredType(members, closing.DeclaredType))
                    members.Add(closing);
            }

            foreach (var key in groupOrder)
            {
                List<ClosedStageModel> members = groups[key];
                if (members.Count < 2)
                    continue;

                Type stageClass = GetStageClass(members[0].ClosedType);

                Type[] declaredTypes = new Type[members.Count];
                for (int i = 0; i < members.Count; i++)
                    declaredTypes[i] = members[i].DeclaredType;

                if (!reported.Add(new DeclarationSet(declaredTypes)))
                    continue;

                // The stage class, not the request: the group is reported once, so a subject
                // taken from the request would follow scan order.
                yield return new RequestFlowValidationProblem(
                    ProblemCodes.AliasedStage,
                    BuildMessage(request.RequestType, members, chainRuns[key], oneChain),
                    stageClass);
            }
        }
    }

    private static Type GetStageClass(Type closedType)
        => closedType.IsGenericType ? closedType.GetGenericTypeDefinition() : closedType;

    // A request with more than one handler has one chain per handler, so the closings the model
    // holds never all run together and only the repeat itself is certain.
    private string BuildMessage(
        Type requestType, List<ClosedStageModel> members, int chainRuns, bool oneChain)
    {
        string callName = _facts.GetFamily(
            members[0].DeclaredType,
            members[0].ContractType).CallName;

        string runs;
        string fix = $"keep one of the {callName} calls and remove the rest.";
        if (!oneChain)
        {
            runs = "more than once";
        }
        else if (chainRuns == 2)
        {
            runs = "twice";
            fix = $"remove one of the two {callName} calls.";
        }
        else
        {
            runs = $"{chainRuns} times";
        }

        if (members.Count == 2)
        {
            ClosedStageModel first = members[0];
            ClosedStageModel second = members[1];

            return first.ClosedType == second.ClosedType
                ? $"Stages '{first.DeclaredType.FullName}' and '{second.DeclaredType.FullName}' both resolve to '{second.ClosedType.FullName}' for request " +
                  $"'{requestType.FullName}' and would run {runs} in the same chain; {fix}"
                : $"Stages '{first.DeclaredType.FullName}' and '{second.DeclaredType.FullName}' are the same stage class " +
                  $"and both apply to request '{requestType.FullName}'; the class would run {runs} in the same chain; {fix}";
        }

        return $"Stages {FormatDeclaredNames(members)} are the same stage class and all apply to request " +
            $"'{requestType.FullName}'; the class would run {runs} in the same chain; {fix}";
    }

    private static string FormatDeclaredNames(List<ClosedStageModel> members)
    {
        var names = new StringBuilder();
        for (int i = 0; i < members.Count; i++)
        {
            if (i > 0)
                names.Append(i == members.Count - 1 ? " and " : ", ");

            names.Append('\'').Append(members[i].DeclaredType.FullName).Append('\'');
        }

        return names.ToString();
    }

    private static bool ContainsDeclaredType(List<ClosedStageModel> members, Type declaredType)
    {
        foreach (var member in members)
        {
            if (member.DeclaredType == declaredType)
                return true;
        }

        return false;
    }

    // Arrays need element-wise equality; a record struct would compare their references.
    private readonly struct DeclarationSet : IEquatable<DeclarationSet>
    {
        private readonly Type[] _declaredTypes;

        public DeclarationSet(Type[] declaredTypes)
            => _declaredTypes = declaredTypes;

        public bool Equals(DeclarationSet other)
        {
            if (_declaredTypes.Length != other._declaredTypes.Length)
                return false;

            for (int i = 0; i < _declaredTypes.Length; i++)
            {
                if (_declaredTypes[i] != other._declaredTypes[i])
                    return false;
            }

            return true;
        }

        public override bool Equals(object? obj)
            => obj is DeclarationSet other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (var type in _declaredTypes)
                    hash = (hash * 397) ^ type.GetHashCode();

                return hash;
            }
        }
    }
}
