using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

// Reaches the internal constructors through the InternalsVisibleTo grant in
// RequestFlow.Abstractions.csproj. The construction contract is what this file tests, so it
// stays on the constructors while every rule test moves to RequestFlowModelBuilder.
public sealed class RequestFlowModelTests
{
    [Fact]
    public void Given_No_Response_Type_When_Creating_Handler_Model_Then_Contract_Is_The_Void_Handler()
    {
        var handler = new HandlerModel(typeof(object));

        handler.ContractType.ShouldBe(typeof(IRequestHandler<>));
    }

    [Fact]
    public void Given_A_Response_Type_When_Creating_Handler_Model_Then_Contract_Is_The_Typed_Handler()
    {
        var handler = new HandlerModel(typeof(object), typeof(int));

        handler.ContractType.ShouldBe(typeof(IRequestHandler<,>));
    }

    [Fact]
    public void Given_No_Contract_When_Creating_Stage_Declaration_Model_Then_Contract_Is_The_Typed_Stage()
    {
        var stage = new StageDeclarationModel(typeof(object), RequestFlowLifetime.Transient, []);

        stage.ContractType.ShouldBe(typeof(IRequestStage<,>));
    }

    [Fact]
    public void Given_No_Contract_When_Creating_Closed_Stage_Model_Then_Contract_Is_The_Typed_Stage()
    {
        var closing = new ClosedStageModel(typeof(object), typeof(string));

        closing.ContractType.ShouldBe(typeof(IRequestStage<,>));
    }

    [Fact]
    public void Given_A_Satellite_Contract_When_Creating_Handler_Model_Then_It_Is_Kept_Unchanged()
    {
        var handler = new HandlerModel(typeof(object), typeof(int), typeof(IAuditedHandler<,>));

        handler.ContractType.ShouldBe(typeof(IAuditedHandler<,>));
    }

    [Fact]
    public void Given_Null_Requests_When_Creating_Model_Then_Throws_Argument_Null_Exception()
    {
        Should.Throw<ArgumentNullException>(() => new RequestFlowModel(null!, []));
    }

    [Fact]
    public void Given_Null_Stage_Declarations_When_Creating_Model_Then_Throws_Argument_Null_Exception()
    {
        Should.Throw<ArgumentNullException>(() => new RequestFlowModel([], null!));
    }

    [Fact]
    public void Given_No_Lifetime_When_Creating_Handler_Model_Then_It_Is_Transient()
    {
        var handler = new HandlerModel(typeof(object), typeof(int));

        handler.Lifetime.ShouldBe(RequestFlowLifetime.Transient);
    }

    [Fact]
    public void Given_Null_Request_Type_When_Creating_Request_Model_Then_Throws_Argument_Null_Exception()
    {
        Should.Throw<ArgumentNullException>(() => new RequestModel(null!, [], []));
    }

    [Fact]
    public void Given_A_Model_When_A_Rule_Casts_Its_Lists_Then_They_Reject_Mutation()
    {
        var request = new RequestModel(
            typeof(int), [new HandlerModel(typeof(object))], []);

        var model = new RequestFlowModel(
            [request], [new StageDeclarationModel(typeof(string), RequestFlowLifetime.Transient, [])]);

        Should.Throw<NotSupportedException>(() => ((IList<RequestModel>)model.Requests).Clear());
        Should.Throw<NotSupportedException>(() => ((IList<StageDeclarationModel>)model.StageDeclarations).Clear());
        Should.Throw<NotSupportedException>(() => ((IList<HandlerModel>)request.Handlers).Clear());
        Should.Throw<NotSupportedException>(() => ((IList<ClosedStageModel>)request.Stages).Clear());
    }

    [Fact]
    public void Given_Parts_When_Creating_Model_Then_Properties_Round_Trip()
    {
        var handler = new HandlerModel(typeof(object), typeof(int));
        var closing = new ClosedStageModel(typeof(object), typeof(string));
        var request = new RequestModel(typeof(int), [handler], [closing]);
        var stage = new StageDeclarationModel(typeof(string), RequestFlowLifetime.Transient, []);

        var model = new RequestFlowModel([request], [stage]);

        model.Requests.ShouldBe([request]);
        model.StageDeclarations.ShouldBe([stage]);
        request.Handlers.ShouldBe([handler]);
        request.Stages.ShouldBe([closing]);
        closing.DeclaredType.ShouldBe(typeof(object));
        closing.ClosedType.ShouldBe(typeof(string));
    }

    [Fact]
    public void Given_No_Response_Type_When_Creating_Handler_Model_Then_It_Is_Void()
    {
        var handler = new HandlerModel(typeof(object));

        handler.IsVoid.ShouldBeTrue();
        handler.ResponseType.ShouldBeNull();
    }

    [Fact]
    public void Given_A_Response_Type_When_Creating_Handler_Model_Then_It_Is_Not_Void()
    {
        var handler = new HandlerModel(typeof(object), typeof(int));

        handler.IsVoid.ShouldBeFalse();
    }

    [Fact]
    public void Given_Reached_Requests_When_Creating_Stage_Declaration_Model_Then_They_Keep_Their_Order()
    {
        var stage = new StageDeclarationModel(
            typeof(object), RequestFlowLifetime.Transient, [typeof(long), typeof(int)]);

        stage.ReachedRequests.ShouldBe([typeof(long), typeof(int)]);
    }

    [Fact]
    public void Given_No_Reached_Requests_When_Creating_Stage_Declaration_Model_Then_The_List_Is_Empty()
    {
        var stage = new StageDeclarationModel(typeof(object), RequestFlowLifetime.Transient, []);

        stage.ReachedRequests.ShouldBeEmpty();
    }

    [Fact]
    public void Given_Null_Reached_Requests_When_Creating_Stage_Declaration_Model_Then_Throws_Argument_Null_Exception()
    {
        Should.Throw<ArgumentNullException>(
            () => new StageDeclarationModel(typeof(object), RequestFlowLifetime.Transient, null!));
    }

    [Fact]
    public void Given_A_Declaration_When_A_Rule_Casts_Its_Reached_Requests_Then_They_Reject_Mutation()
    {
        var stage = new StageDeclarationModel(typeof(object), RequestFlowLifetime.Transient, [typeof(int)]);

        Should.Throw<NotSupportedException>(() => ((IList<Type>)stage.ReachedRequests).Clear());
    }

    // The void contracts are separate interfaces rather than derivations of the two-parameter ones,
    // so naming one has to be accepted on its own.
    [Fact]
    public void Given_The_Void_Contract_On_A_Typed_Handler_When_Creating_Handler_Model_Then_It_Is_Kept()
    {
        var handler = new HandlerModel(typeof(object), typeof(int), typeof(IRequestHandler<>));

        handler.ContractType.ShouldBe(typeof(IRequestHandler<>));
    }

    [Fact]
    public void Given_The_Typed_Contract_On_A_Void_Handler_When_Creating_Handler_Model_Then_It_Is_Kept()
    {
        var handler = new HandlerModel(
            typeof(object), responseType: null, contractType: typeof(IRequestHandler<,>));

        handler.ContractType.ShouldBe(typeof(IRequestHandler<,>));
    }

    [Fact]
    public void Given_The_Void_Contract_When_Creating_Stage_Declaration_Model_Then_It_Is_Kept()
    {
        var stage = new StageDeclarationModel(
            typeof(object), RequestFlowLifetime.Transient, [], typeof(IRequestStage<>));

        stage.ContractType.ShouldBe(typeof(IRequestStage<>));
    }

    [Fact]
    public void Given_The_Void_Contract_When_Creating_Closed_Stage_Model_Then_It_Is_Kept()
    {
        var closing = new ClosedStageModel(typeof(object), typeof(string), typeof(IRequestStage<>));

        closing.ContractType.ShouldBe(typeof(IRequestStage<>));
    }

    [Fact]
    public void Given_A_Satellite_Contract_When_Creating_Closed_Stage_Model_Then_It_Is_Kept_Unchanged()
    {
        var closing = new ClosedStageModel(typeof(object), typeof(string), typeof(IAuditedStage<,>));

        closing.ContractType.ShouldBe(typeof(IAuditedStage<,>));
    }

    // A contract from a family this package cannot name, such as a stream or event contract, is
    // kept as given. The check that survives is the shape of the type.
    [Fact]
    public void Given_An_Unrelated_Open_Interface_When_Creating_Handler_Model_Then_It_Is_Kept_Unchanged()
    {
        var handler = new HandlerModel(typeof(object), typeof(int), typeof(IEnumerable<>));

        handler.ContractType.ShouldBe(typeof(IEnumerable<>));
    }

    [Fact]
    public void Given_An_Unrelated_Open_Interface_When_Creating_Stage_Declaration_Model_Then_It_Is_Kept_Unchanged()
    {
        var declaration = new StageDeclarationModel(
            typeof(object), RequestFlowLifetime.Transient, [], typeof(IEnumerable<>));

        declaration.ContractType.ShouldBe(typeof(IEnumerable<>));
    }

    [Fact]
    public void Given_An_Unrelated_Open_Interface_When_Creating_Closed_Stage_Model_Then_It_Is_Kept_Unchanged()
    {
        var closing = new ClosedStageModel(typeof(object), typeof(string), typeof(IEnumerable<>));

        closing.ContractType.ShouldBe(typeof(IEnumerable<>));
    }

    [Fact]
    public void Given_A_Closed_Interface_When_Creating_Handler_Model_Then_Throws_Naming_The_Contract()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => new HandlerModel(typeof(object), typeof(int), typeof(IEquatable<int>)));

        exception.ParamName.ShouldBe("contractType");
    }

    [Fact]
    public void Given_A_Class_When_Creating_Handler_Model_Then_Throws_Naming_The_Contract()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => new HandlerModel(typeof(object), typeof(int), typeof(string)));

        exception.ParamName.ShouldBe("contractType");
    }

    [Fact]
    public void Given_A_Closed_Interface_When_Creating_Stage_Declaration_Model_Then_Throws_Naming_The_Contract()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => new StageDeclarationModel(
                typeof(object), RequestFlowLifetime.Transient, [], typeof(IEquatable<int>)));

        exception.ParamName.ShouldBe("contractType");
    }

    [Fact]
    public void Given_A_Class_When_Creating_Stage_Declaration_Model_Then_Throws_Naming_The_Contract()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => new StageDeclarationModel(
                typeof(object), RequestFlowLifetime.Transient, [], typeof(string)));

        exception.ParamName.ShouldBe("contractType");
    }

    [Fact]
    public void Given_A_Closed_Interface_When_Creating_Closed_Stage_Model_Then_Throws_Naming_The_Contract()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => new ClosedStageModel(typeof(object), typeof(string), typeof(IEquatable<int>)));

        exception.ParamName.ShouldBe("contractType");
    }

    [Fact]
    public void Given_A_Class_When_Creating_Closed_Stage_Model_Then_Throws_Naming_The_Contract()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => new ClosedStageModel(typeof(object), typeof(string), typeof(string)));

        exception.ParamName.ShouldBe("contractType");
    }

    #region Helpers

    // Contracts of the kind a package adds on top of the core ones.
    private interface IAuditedHandler<in TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    { }

    private interface IAuditedStage<in TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    { }

    #endregion
}
