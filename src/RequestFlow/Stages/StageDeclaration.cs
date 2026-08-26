using System;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

/// <summary>
/// One registered stage: the stage type, the family it belongs to, the optional handler contract
/// that narrows which requests it reaches, and its lifetime. Position in the registry's list is execution order.
/// </summary>
internal sealed class StageDeclaration(
    Type stageType, Type? handlerFilter, StageFamily family, ServiceLifetime lifetime = ServiceLifetime.Transient)
{
    public Type StageType { get; } = stageType;

    public Type? HandlerFilter { get; } = handlerFilter;

    public StageFamily Family { get; } = family;

    public ServiceLifetime Lifetime { get; } = lifetime;
}

/// <summary>
/// The contracts one kind of stage is written against, and the handler contracts it can wrap.
/// The two families never mix: a stream declaration cannot close over a task handler, and the reverse is equally impossible.
/// </summary>
internal sealed class StageFamily
{
    public static readonly StageFamily Request = new(
        typedContract: typeof(IRequestStage<,>),
        voidContract: typeof(IRequestStage<>),
        handlerContracts: [typeof(IRequestHandler<,>), typeof(IRequestHandler<>)],
        contractName: "IRequestStage",
        contractList: "IRequestStage<TRequest, TResponse> or IRequestStage<TRequest>",
        callName: "AddStage",
        missingAdvice: "implement one of them or remove the AddStage call.",
        parameterAdvice:
            "An open generic stage implements IRequestStage<TRequest, TResponse> with its own two " +
            "parameters in that order, or declares one parameter and uses it as the request: " +
            "IRequestStage<TRequest> for void requests, or IRequestStage<TRequest, TResponse> with a fixed response type.");

    public static readonly StageFamily Stream = new(
        typedContract: typeof(IStreamRequestStage<,>),
        voidContract: null,
        handlerContracts: [typeof(IStreamRequestHandler<,>)],
        contractName: "IStreamRequestStage",
        contractList: "IStreamRequestStage<TRequest, TItem>",
        callName: "AddStreamStage",
        missingAdvice: "implement it or remove the AddStreamStage call.",
        parameterAdvice:
            "An open generic stream stage implements IStreamRequestStage<TRequest, TItem> with its " +
            "own two parameters in that order, or declares one parameter and uses it as the " +
            "request, with the item type fixed by the class.");

    private readonly Type[] _handlerContracts;
    private readonly string _contractName;
    private readonly string _contractList;
    private readonly string _missingAdvice;
    private readonly string _parameterAdvice;

    private StageFamily(
        Type typedContract,
        Type? voidContract,
        Type[] handlerContracts,
        string contractName,
        string contractList,
        string callName,
        string missingAdvice,
        string parameterAdvice)
    {
        TypedContract = typedContract;
        VoidContract = voidContract;
        Contracts = voidContract is null ? [typedContract] : [typedContract, voidContract];
        _handlerContracts = handlerContracts;
        _contractName = contractName;
        _contractList = contractList;
        CallName = callName;
        _missingAdvice = missingAdvice;
        _parameterAdvice = parameterAdvice;
    }

    /// <summary>
    /// The family whose typed or void contract <paramref name="contractType"/> implements or
    /// derives from. Falls back to <see cref="Request"/>, the only family left once
    /// <see cref="Stream"/> is ruled out.
    /// </summary>
    public static StageFamily FromContract(Type contractType)
        => StageContract.Implements(contractType, Stream.TypedContract) ? Stream : Request;

    /// <summary>
    /// The two-argument contract, which every stage in the family can be written against.
    /// </summary>
    public Type TypedContract { get; }

    /// <summary>
    /// The one-argument contract, or null for a family that has no void form.
    /// </summary>
    public Type? VoidContract { get; }

    /// <summary>
    /// Every contract in the family, for the shape checks that accept any of them.
    /// </summary>
    public Type[] Contracts { get; }

    /// <summary>
    /// The registration call that adds a stage of this family: <c>AddStage</c> or <c>AddStreamStage</c>.
    /// </summary>
    public string CallName { get; }

    /// <summary>
    /// True when a handler discovered through <paramref name="handlerContractDefinition"/> can be wrapped by a stage of this family.
    /// </summary>
    public bool Handles(Type handlerContractDefinition)
    {
        foreach (var contract in _handlerContracts)
        {
            if (contract == handlerContractDefinition)
                return true;
        }

        return false;
    }

    public string BuildMissingContractMessage(Type stageType)
        => $"'{stageType.FullName}' does not implement {_contractList}; {_missingAdvice}";

    public string BuildParametersMisusedMessage(Type stageType, string parameterNames)
        => $"'{stageType.FullName}' declares generic parameters <{parameterNames}> that its {_contractName} implementation does not use as its request. {_parameterAdvice}";
}
