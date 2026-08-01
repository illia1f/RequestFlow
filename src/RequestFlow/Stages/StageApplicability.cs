using System;

namespace RequestFlow;

/// <summary>
/// Narrows the requests a stage applies to beyond what the stage's own generic constraints express.
/// </summary>
public sealed class StageApplicability
{
    internal Type? HandlerFilter { get; private set; }

    /// <summary>
    /// Limits the stage to requests whose handler implements <typeparamref name="TContract"/>.
    /// One filter per stage: a second call throws rather than replace the first.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public StageApplicability WhereHandlerImplements<TContract>()
    {
        if (HandlerFilter is not null)
        {
            throw new InvalidOperationException(
                $"This stage already filters on '{HandlerFilter.FullName}'. A stage takes one handler filter; " +
                "to reach handlers of several contracts, give them one shared contract to implement.");
        }

        HandlerFilter = typeof(TContract);
        return this;
    }
}
