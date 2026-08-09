using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reports which requests one stage declaration reached.
/// </summary>
/// <remarks>
/// The single source for <see cref="StageDeclarationModel.ReachedRequests"/>, so a model the
/// library froze and one a test built by hand derive the reach the same way. Needs the requests
/// already built, since the reach is read off their chains.
/// </remarks>
internal static class StageReach
{
    public static Type[] Of(IReadOnlyList<RequestModel> requests, Type stageType)
    {
        List<Type> reached = [];
        foreach (var request in requests)
        {
            foreach (var stage in request.Stages)
            {
                if (stage.DeclaredType == stageType)
                {
                    reached.Add(request.RequestType);
                    break;
                }
            }
        }

        return [.. reached];
    }
}
