using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class RequestFlowModelBuilderTests
{
    [Fact]
    public void Given_Nothing_Added_When_Building_Then_Both_Lists_Are_Empty()
    {
        RequestFlowModel model = new RequestFlowModelBuilder().Build();

        model.Requests.ShouldBeEmpty();
        model.StageDeclarations.ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Request_With_A_Handler_When_Building_Then_The_Parts_Round_Trip()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), r => r.AddHandler(typeof(object), typeof(string)))
            .AddStageDeclaration(typeof(string))
            .Build();

        RequestModel request = model.Requests.ShouldHaveSingleItem();
        request.RequestType.ShouldBe(typeof(int));

        HandlerModel handler = request.Handlers.ShouldHaveSingleItem();
        handler.HandlerType.ShouldBe(typeof(object));
        handler.ResponseType.ShouldBe(typeof(string));
        handler.ContractType.ShouldBe(typeof(IRequestHandler<,>));

        model.StageDeclarations.ShouldHaveSingleItem().StageType.ShouldBe(typeof(string));
    }

    [Fact]
    public void Given_Two_Calls_For_One_Request_Type_When_Building_Then_One_Entry_Holds_Both_Handlers()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), r => r.AddHandler(typeof(object), typeof(string)))
            .AddRequest(typeof(int), r => r.AddHandler(typeof(Uri), typeof(string)))
            .Build();

        model.Requests.ShouldHaveSingleItem().Handlers.Count.ShouldBe(2);
    }

    [Fact]
    public void Given_Requests_Added_In_Order_When_Building_Then_That_Order_Is_Kept()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddRequest(typeof(long))
            .AddRequest(typeof(int))
            .AddRequest(typeof(long))
            .Build();

        model.Requests.Select(r => r.RequestType).ShouldBe([typeof(long), typeof(int)]);
    }

    [Fact]
    public void Given_No_Configure_Callback_When_Adding_A_Request_Then_It_Has_No_Handlers_Or_Stages()
    {
        RequestFlowModel model = new RequestFlowModelBuilder().AddRequest(typeof(int)).Build();

        RequestModel request = model.Requests.ShouldHaveSingleItem();
        request.Handlers.ShouldBeEmpty();
        request.Stages.ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Class_As_The_Contract_When_Adding_A_Stage_Declaration_Then_Throws_Argument_Exception()
    {
        var sut = new RequestFlowModelBuilder();

        Should.Throw<ArgumentException>(() => sut.AddStageDeclaration(typeof(object), typeof(string)));
    }

    [Fact]
    public void Given_A_Closed_Interface_As_The_Contract_When_Adding_A_Stage_Declaration_Then_Throws_Argument_Exception()
    {
        var sut = new RequestFlowModelBuilder();

        Should.Throw<ArgumentException>(() => sut.AddStageDeclaration(typeof(object), typeof(IEquatable<int>)));
    }

    [Fact]
    public void Given_A_Class_As_The_Contract_When_Adding_A_Stage_Then_Throws_Argument_Exception()
    {
        var sut = new RequestFlowModelBuilder();

        Should.Throw<ArgumentException>(
            () => sut.AddRequest(typeof(int), r => r.AddStage(typeof(object), typeof(object), typeof(string))));
    }

    [Fact]
    public void Given_A_Class_As_The_Contract_When_Adding_A_Handler_Then_Throws_Argument_Exception()
    {
        var sut = new RequestFlowModelBuilder();

        Should.Throw<ArgumentException>(
            () => sut.AddRequest(typeof(int), r => r.AddHandler(typeof(object), typeof(int), typeof(string))));
    }

    [Fact]
    public void Given_A_Null_Request_Type_When_Adding_A_Request_Then_Throws_Argument_Null_Exception()
    {
        var sut = new RequestFlowModelBuilder();

        Should.Throw<ArgumentNullException>(() => sut.AddRequest(null!));
    }

    [Fact]
    public void Given_A_Null_Stage_Type_When_Adding_A_Stage_Then_Throws_Argument_Null_Exception()
    {
        var sut = new RequestFlowModelBuilder();

        Should.Throw<ArgumentNullException>(() => sut.AddStageDeclaration(null!));
    }

    [Fact]
    public void Given_A_Null_Handler_Type_When_Adding_A_Handler_Then_Throws_Argument_Null_Exception()
    {
        var sut = new RequestFlowModelBuilder();

        Should.Throw<ArgumentNullException>(() => sut.AddRequest(typeof(int), r => r.AddHandler(null!)));
    }

    [Theory]
    [InlineData(RequestFlowLifetime.Transient)]
    [InlineData(RequestFlowLifetime.Scoped)]
    [InlineData(RequestFlowLifetime.Singleton)]
    public void Given_A_Handler_Added_With_A_Lifetime_When_Building_Then_It_Is_Recorded(RequestFlowLifetime lifetime)
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), r => r.AddHandler(typeof(object), lifetime, typeof(string)))
            .Build();

        HandlerModel handler = model.Requests.ShouldHaveSingleItem().Handlers.ShouldHaveSingleItem();
        handler.Lifetime.ShouldBe(lifetime);
        handler.ResponseType.ShouldBe(typeof(string));
    }

    [Fact]
    public void Given_Handlers_Added_With_Different_Lifetimes_When_Building_Then_Each_Keeps_Its_Own()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddRequest(
                typeof(int),
                r => r
                    .AddHandler(typeof(object), RequestFlowLifetime.Scoped, typeof(string))
                    .AddHandler(typeof(string), RequestFlowLifetime.Transient, typeof(string)))
            .Build();

        IReadOnlyList<HandlerModel> handlers = model.Requests.ShouldHaveSingleItem().Handlers;
        handlers[0].Lifetime.ShouldBe(RequestFlowLifetime.Scoped);
        handlers[1].Lifetime.ShouldBe(RequestFlowLifetime.Transient);
    }

    // A response type in the second slot still binds the overload that has no lifetime.
    [Fact]
    public void Given_A_Handler_Added_Without_A_Lifetime_When_Building_Then_It_Is_Transient()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), r => r.AddHandler(typeof(object), typeof(string)))
            .Build();

        HandlerModel handler = model.Requests.ShouldHaveSingleItem().Handlers.ShouldHaveSingleItem();
        handler.Lifetime.ShouldBe(RequestFlowLifetime.Transient);
        handler.ResponseType.ShouldBe(typeof(string));
    }

    [Fact]
    public void Given_A_Handler_Added_With_A_Response_Type_And_A_Contract_When_Building_Then_Both_Are_Recorded()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddRequest(
                typeof(int),
                r => r.AddHandler(typeof(object), typeof(string), typeof(IAuditedHandler<,>)))
            .Build();

        HandlerModel handler = model.Requests.ShouldHaveSingleItem().Handlers.ShouldHaveSingleItem();
        handler.ResponseType.ShouldBe(typeof(string));
        handler.ContractType.ShouldBe(typeof(IAuditedHandler<,>));
    }

    [Fact]
    public void Given_A_Stage_Declaration_Added_With_A_Lifetime_When_Building_Then_That_Lifetime_Is_Recorded()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddStageDeclaration(typeof(object), RequestFlowLifetime.Singleton)
            .Build();

        model.StageDeclarations.ShouldHaveSingleItem().Lifetime.ShouldBe(RequestFlowLifetime.Singleton);
    }

    [Fact]
    public void Given_A_Stage_Declaration_Added_With_A_Lifetime_And_A_Contract_When_Building_Then_Both_Are_Recorded()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddStageDeclaration(typeof(object), RequestFlowLifetime.Scoped, typeof(IAuditedStage<,>))
            .Build();

        StageDeclarationModel declaration = model.StageDeclarations.ShouldHaveSingleItem();
        declaration.Lifetime.ShouldBe(RequestFlowLifetime.Scoped);
        declaration.ContractType.ShouldBe(typeof(IAuditedStage<,>));
    }

    // A contract in the second slot still binds the overload that has no lifetime.
    [Fact]
    public void Given_A_Stage_Declaration_Added_With_A_Contract_Only_When_Building_Then_It_Is_Transient()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddStageDeclaration(typeof(object), typeof(IAuditedStage<,>))
            .Build();

        StageDeclarationModel declaration = model.StageDeclarations.ShouldHaveSingleItem();
        declaration.ContractType.ShouldBe(typeof(IAuditedStage<,>));
        declaration.Lifetime.ShouldBe(RequestFlowLifetime.Transient);
    }

    [Fact]
    public void Given_A_Stage_In_Two_Chains_When_Building_Then_The_Declaration_Reached_Both_In_Request_Order()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddRequest(typeof(long), r => r.AddStage(typeof(object), typeof(string)))
            .AddRequest(typeof(int), r => r.AddStage(typeof(object), typeof(string)))
            .AddStageDeclaration(typeof(object))
            .Build();

        model.StageDeclarations.ShouldHaveSingleItem()
            .ReachedRequests.ShouldBe([typeof(long), typeof(int)]);
    }

    [Fact]
    public void Given_A_Declaration_No_Chain_Names_When_Building_Then_It_Reached_Nothing()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddRequest(typeof(int), r => r.AddStage(typeof(object), typeof(string)))
            .AddStageDeclaration(typeof(Uri))
            .Build();

        model.StageDeclarations.ShouldHaveSingleItem().ReachedRequests.ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Request_With_An_Empty_Chain_When_Building_Then_The_Declaration_Does_Not_Claim_It()
    {
        RequestFlowModel model = new RequestFlowModelBuilder()
            .AddRequest(typeof(long), r => r.AddStage(typeof(object), typeof(string)))
            .AddRequest(typeof(int))
            .AddStageDeclaration(typeof(object))
            .Build();

        model.StageDeclarations.ShouldHaveSingleItem().ReachedRequests.ShouldBe([typeof(long)]);
    }

    [Fact]
    public void Given_A_Builder_Built_Twice_When_Comparing_The_Models_Then_They_Share_No_Instance()
    {
        var sut = new RequestFlowModelBuilder();
        sut.AddRequest(typeof(int), r => r.AddHandler(typeof(object), typeof(string)));
        sut.AddStageDeclaration(typeof(Uri));

        RequestFlowModel first = sut.Build();
        RequestFlowModel second = sut.Build();

        second.ShouldNotBeSameAs(first);
        second.Requests.ShouldNotBeSameAs(first.Requests);
        second.Requests[0].ShouldNotBeSameAs(first.Requests[0]);
        second.StageDeclarations.ShouldNotBeSameAs(first.StageDeclarations);
        second.StageDeclarations[0].ShouldNotBeSameAs(first.StageDeclarations[0]);
    }

    // The builder hands each model its own arrays, so what it collects afterwards cannot reach a
    // model already handed out.
    [Fact]
    public void Given_A_Built_Model_When_More_Is_Added_And_Built_Again_Then_The_First_Model_Is_Unchanged()
    {
        var sut = new RequestFlowModelBuilder();
        sut.AddRequest(typeof(int), r => r.AddHandler(typeof(object), typeof(string)));
        sut.AddStageDeclaration(typeof(Uri));
        RequestFlowModel first = sut.Build();

        sut.AddRequest(typeof(int), r => r.AddHandler(typeof(Uri), typeof(string)));
        sut.AddStageDeclaration(typeof(Uri));
        RequestFlowModel second = sut.Build();

        first.Requests.ShouldHaveSingleItem().Handlers.Count.ShouldBe(1);
        first.StageDeclarations.Count.ShouldBe(1);
        second.Requests.ShouldHaveSingleItem().Handlers.Count.ShouldBe(2);
        second.StageDeclarations.Count.ShouldBe(2);
    }

    #region Helpers

    // Contracts of the kind a package adds on top of the core ones.
    private interface IAuditedStage<in TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    { }

    private interface IAuditedHandler<in TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    { }

    #endregion
}
