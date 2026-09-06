using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class RegistrationSnapshotTests
{
    [Fact]
    public void Given_Scanned_Request_Without_Handler_When_Building_Model_Then_Request_Has_No_Handlers()
    {
        RequestFlowModel model = RegistrationSnapshot.Capture(
            handlers: [], requestTypes: [typeof(Ping)], stageDeclarations: [], closings: new StageClosingCache()).Model;

        RequestModel request = model.Requests.ShouldHaveSingleItem();
        request.RequestType.ShouldBe(typeof(Ping));
        request.Handlers.ShouldBeEmpty();
        request.Stages.ShouldBeEmpty();
    }

    [Fact]
    public void Given_Handler_For_Unscanned_Request_When_Building_Model_Then_Request_Is_Included()
    {
        HandlerRegistration registration = Handler(
            typeof(PingHandler), typeof(Ping), typeof(string), typeof(IRequestHandler<Ping, string>));

        RequestFlowModel model = RegistrationSnapshot.Capture(
            handlers: [registration], requestTypes: [], stageDeclarations: [], closings: new StageClosingCache()).Model;

        RequestModel request = model.Requests.ShouldHaveSingleItem();
        HandlerModel handler = request.Handlers.ShouldHaveSingleItem();
        handler.HandlerType.ShouldBe(typeof(PingHandler));
        handler.ResponseType.ShouldBe(typeof(string));
    }

    [Fact]
    public void Given_Duplicate_Handlers_When_Building_Model_Then_Both_Are_Listed()
    {
        HandlerRegistration first = Handler(
            typeof(PingHandler), typeof(Ping), typeof(string), typeof(IRequestHandler<Ping, string>));
        HandlerRegistration second = Handler(
            typeof(SecondPingHandler), typeof(Ping), typeof(string), typeof(IRequestHandler<Ping, string>));

        RequestFlowModel model = RegistrationSnapshot.Capture(
            handlers: [first, second], requestTypes: [typeof(Ping)], stageDeclarations: [], closings: new StageClosingCache()).Model;

        model.Requests.ShouldHaveSingleItem().Handlers.Count.ShouldBe(2);
    }

    [Fact]
    public void Given_Applicable_Stage_When_Building_Model_Then_Chain_Names_Declaration_And_Closed_Type()
    {
        HandlerRegistration registration = Handler(
            typeof(PingHandler), typeof(Ping), typeof(string), typeof(IRequestHandler<Ping, string>));
        var declaration = new StageDeclaration(typeof(WrapStage<,>), handlerFilter: null, StageFamily.Request);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            handlers: [registration], requestTypes: [typeof(Ping)], stageDeclarations: [declaration],
            closings: new StageClosingCache()).Model;

        ClosedStageModel closing = model.Requests.ShouldHaveSingleItem().Stages.ShouldHaveSingleItem();
        closing.DeclaredType.ShouldBe(typeof(WrapStage<,>));
        closing.ClosedType.ShouldBe(typeof(WrapStage<Ping, string>));
    }

    [Fact]
    public void Given_Stage_Declarations_When_Building_Model_Then_Stages_Keep_Registration_Order_And_Duplicates()
    {
        var first = new StageDeclaration(typeof(WrapStage<,>), handlerFilter: null, StageFamily.Request);
        var second = new StageDeclaration(typeof(WrapStage<,>), handlerFilter: null, StageFamily.Request);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            handlers: [], requestTypes: [], stageDeclarations: [first, second], closings: new StageClosingCache()).Model;

        model.StageDeclarations.Count.ShouldBe(2);
        model.StageDeclarations[0].StageType.ShouldBe(typeof(WrapStage<,>));
    }

    [Fact]
    public void Given_Scanned_And_Handler_Only_Requests_When_Building_Model_Then_Scanned_Request_Comes_First()
    {
        HandlerRegistration registration = Handler(
            typeof(PingHandler), typeof(Ping), typeof(string), typeof(IRequestHandler<Ping, string>));

        RequestFlowModel model = RegistrationSnapshot.Capture(
            handlers: [registration], requestTypes: [typeof(Purge)], stageDeclarations: [], closings: new StageClosingCache()).Model;

        model.Requests.Count.ShouldBe(2);
        model.Requests[0].RequestType.ShouldBe(typeof(Purge));
        model.Requests[1].RequestType.ShouldBe(typeof(Ping));
    }

    [Fact]
    public void Given_Duplicate_Handlers_And_A_Stage_When_Building_Model_Then_Chain_Holds_One_Closing_Per_Handler()
    {
        HandlerRegistration first = Handler(
            typeof(MultiPingStringHandler), typeof(MultiPing), typeof(string), typeof(IRequestHandler<MultiPing, string>));
        HandlerRegistration second = Handler(
            typeof(MultiPingIntHandler), typeof(MultiPing), typeof(int), typeof(IRequestHandler<MultiPing, int>));
        var declaration = new StageDeclaration(typeof(WrapStage<,>), handlerFilter: null, StageFamily.Request);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            handlers: [first, second], requestTypes: [typeof(MultiPing)], stageDeclarations: [declaration],
            closings: new StageClosingCache()).Model;

        IReadOnlyList<ClosedStageModel> chain = model.Requests.ShouldHaveSingleItem().Stages;
        chain.Count.ShouldBe(2);
        chain[0].ClosedType.ShouldBe(typeof(WrapStage<MultiPing, string>));
        chain[1].ClosedType.ShouldBe(typeof(WrapStage<MultiPing, int>));
    }

    [Fact]
    public void Given_Stage_Closing_Only_Over_The_Second_Handler_When_Building_Model_Then_Chain_Includes_It()
    {
        HandlerRegistration first = Handler(
            typeof(MultiPingStringHandler), typeof(MultiPing), typeof(string), typeof(IRequestHandler<MultiPing, string>));
        HandlerRegistration second = Handler(
            typeof(MultiPingIntHandler), typeof(MultiPing), typeof(int), typeof(IRequestHandler<MultiPing, int>));
        var declaration = new StageDeclaration(typeof(IntResultStage<>), handlerFilter: null, StageFamily.Request);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            handlers: [first, second], requestTypes: [typeof(MultiPing)], stageDeclarations: [declaration],
            closings: new StageClosingCache()).Model;

        ClosedStageModel closing = model.Requests.ShouldHaveSingleItem().Stages.ShouldHaveSingleItem();
        closing.ClosedType.ShouldBe(typeof(IntResultStage<MultiPing>));
    }

    [Fact]
    public void Given_Unhandled_Request_And_A_Stage_Declaration_When_Building_Model_Then_Chain_Is_Empty()
    {
        var declaration = new StageDeclaration(typeof(WrapStage<,>), handlerFilter: null, StageFamily.Request);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            handlers: [], requestTypes: [typeof(Ping)], stageDeclarations: [declaration], closings: new StageClosingCache()).Model;

        model.Requests.ShouldHaveSingleItem().Stages.ShouldBeEmpty();
    }

    [Fact]
    public void Given_Distinct_Stage_Declarations_When_Building_Model_Then_Stages_Preserve_Declared_Order()
    {
        var first = new StageDeclaration(typeof(WrapStage<,>), handlerFilter: null, StageFamily.Request);
        var second = new StageDeclaration(typeof(ExtraStage<,>), handlerFilter: null, StageFamily.Request);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            handlers: [], requestTypes: [], stageDeclarations: [first, second], closings: new StageClosingCache()).Model;

        model.StageDeclarations.Count.ShouldBe(2);
        model.StageDeclarations[0].StageType.ShouldBe(typeof(WrapStage<,>));
        model.StageDeclarations[1].StageType.ShouldBe(typeof(ExtraStage<,>));
    }

    [Fact]
    public void Given_Stage_That_Does_Not_Apply_To_The_Handler_When_Building_Model_Then_Chain_Excludes_It()
    {
        HandlerRegistration registration = Handler(
            typeof(PingHandler), typeof(Ping), typeof(string), typeof(IRequestHandler<Ping, string>));
        var declaration = new StageDeclaration(typeof(PurgeOnlyStage), handlerFilter: null, StageFamily.Request);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            handlers: [registration], requestTypes: [typeof(Ping)], stageDeclarations: [declaration],
            closings: new StageClosingCache()).Model;

        model.Requests.ShouldHaveSingleItem().Stages.ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Void_Stage_Declaration_When_Capturing_Then_The_Void_Contract_Is_Recorded()
    {
        HandlerRegistration handler = Handler(
            typeof(VoidHandler), typeof(VoidRequest), typeof(NoResult), typeof(IRequestHandler<VoidRequest>), isVoid: true);
        var declaration = new StageDeclaration(typeof(VoidStage), handlerFilter: null, StageFamily.Request);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(VoidRequest)], [declaration], new StageClosingCache()).Model;

        model.StageDeclarations.ShouldHaveSingleItem().ContractType.ShouldBe(typeof(IRequestStage<>));
        model.Requests.ShouldHaveSingleItem()
            .Stages.ShouldHaveSingleItem().ContractType.ShouldBe(typeof(IRequestStage<>));
    }

    [Fact]
    public void Given_A_Typed_Stage_Declaration_When_Capturing_Then_The_Typed_Contract_Is_Recorded()
    {
        HandlerRegistration handler = Handler(
            typeof(PingHandler), typeof(Ping), typeof(string), typeof(IRequestHandler<Ping, string>));
        var declaration = new StageDeclaration(typeof(WrapStage<,>), handlerFilter: null, StageFamily.Request);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(Ping)], [declaration], new StageClosingCache()).Model;

        model.StageDeclarations.ShouldHaveSingleItem().ContractType.ShouldBe(typeof(IRequestStage<,>));
        model.Requests.ShouldHaveSingleItem()
            .Stages.ShouldHaveSingleItem().ContractType.ShouldBe(typeof(IRequestStage<,>));
    }

    [Fact]
    public void Given_A_Typed_Value_Handler_When_Capturing_Then_The_Value_Handler_Contract_Is_Recorded()
    {
        HandlerRegistration handler = Handler(
            typeof(ValuePingHandler),
            typeof(ValuePing),
            typeof(string),
            typeof(IValueRequestHandler<ValuePing, string>));

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(ValuePing)], [], new StageClosingCache()).Model;

        model.Requests.ShouldHaveSingleItem()
            .Handlers.ShouldHaveSingleItem().ContractType
            .ShouldBe(typeof(IValueRequestHandler<,>));
    }

    [Fact]
    public void Given_A_Plain_Void_Value_Handler_When_Capturing_Then_The_Value_Handler_Contract_Is_Recorded()
    {
        HandlerRegistration handler = Handler(
            typeof(ValueVoidHandler),
            typeof(ValueVoid),
            typeof(NoResult),
            typeof(IValueRequestHandler<ValueVoid>),
            isVoid: true);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(ValueVoid)], [], new StageClosingCache()).Model;

        HandlerModel captured = model.Requests.ShouldHaveSingleItem()
            .Handlers.ShouldHaveSingleItem();
        captured.ContractType.ShouldBe(typeof(IValueRequestHandler<>));
        captured.ResponseType.ShouldBeNull();
    }

    [Fact]
    public void Given_One_Stage_Type_In_Task_And_Value_Families_When_Capturing_Then_Each_Declaration_And_Closing_Retains_Its_Own_Contract()
    {
        HandlerRegistration taskHandler = Handler(
            typeof(TaskMemoHandler),
            typeof(TaskMemo),
            typeof(string),
            typeof(IRequestHandler<TaskMemo, string>));
        HandlerRegistration valueHandler = Handler(
            typeof(ValueMemoHandler),
            typeof(ValueMemo),
            typeof(string),
            typeof(IValueRequestHandler<ValueMemo, string>));
        var taskDeclaration = new StageDeclaration(
            typeof(DualMemoStage), handlerFilter: null, StageFamily.Request);
        var valueDeclaration = new StageDeclaration(
            typeof(DualMemoStage), handlerFilter: null, StageFamily.Value);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [taskHandler, valueHandler],
            [typeof(TaskMemo), typeof(ValueMemo)],
            [taskDeclaration, valueDeclaration],
            new StageClosingCache()).Model;

        model.StageDeclarations[0].ContractType.ShouldBe(typeof(IRequestStage<,>));
        model.StageDeclarations[1].ContractType.ShouldBe(typeof(IValueRequestStage<,>));
        model.Requests[0].Stages.ShouldHaveSingleItem().ContractType
            .ShouldBe(typeof(IRequestStage<,>));
        model.Requests[1].Stages.ShouldHaveSingleItem().ContractType
            .ShouldBe(typeof(IValueRequestStage<,>));
    }

    [Fact]
    public void Given_A_Plain_Void_Value_Stage_When_Capturing_Then_The_Value_Stage_Contract_Is_Recorded()
    {
        HandlerRegistration handler = Handler(
            typeof(ValueVoidHandler),
            typeof(ValueVoid),
            typeof(NoResult),
            typeof(IValueRequestHandler<ValueVoid>),
            isVoid: true);
        var declaration = new StageDeclaration(
            typeof(ValueVoidStage), handlerFilter: null, StageFamily.Value);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(ValueVoid)], [declaration], new StageClosingCache()).Model;

        model.StageDeclarations.ShouldHaveSingleItem().ContractType
            .ShouldBe(typeof(IValueRequestStage<>));
        model.Requests.ShouldHaveSingleItem().Stages.ShouldHaveSingleItem().ContractType
            .ShouldBe(typeof(IValueRequestStage<>));
    }

    [Fact]
    public void Given_A_Value_Handler_Implementing_A_Package_Contract_When_Capturing_Then_The_Derived_Contract_Wins()
    {
        HandlerRegistration handler = Handler(
            typeof(AuditedValueHandler),
            typeof(AuditedValue),
            typeof(string),
            typeof(IValueRequestHandler<AuditedValue, string>));

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(AuditedValue)], [], new StageClosingCache()).Model;

        model.Requests.ShouldHaveSingleItem().Handlers.ShouldHaveSingleItem().ContractType
            .ShouldBe(typeof(IAuditedValueHandler<,>));
    }

    [Fact]
    public void Given_A_Custom_Validation_Rule_When_Validating_Value_Registrations_Then_It_Observes_Exact_Value_Contracts()
    {
        ValueContractCaptureRule.Captured = null;
        var services = new ServiceCollection();
        services.AddRequestFlow(options =>
            {
                options.AddHandler<PublicModelValueHandler>();
                options.AddValueStage<PublicModelValueStage>();
            })
            .AddValidationRule<ValueContractCaptureRule>();
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.ValidateRequestFlow();

        RequestFlowModel model = ValueContractCaptureRule.Captured.ShouldNotBeNull();
        model.Requests.ShouldHaveSingleItem().Handlers.ShouldHaveSingleItem().ContractType
            .ShouldBe(typeof(IValueRequestHandler<,>));
        model.StageDeclarations.ShouldHaveSingleItem().ContractType
            .ShouldBe(typeof(IValueRequestStage<,>));
        model.Requests.ShouldHaveSingleItem().Stages.ShouldHaveSingleItem().ContractType
            .ShouldBe(typeof(IValueRequestStage<,>));
    }

    [Fact]
    public void Given_A_Handler_Implementing_A_Derived_Contract_When_Capturing_Then_The_Derived_Contract_Is_Recorded()
    {
        HandlerRegistration handler = Handler(
            typeof(AuditedHandler), typeof(Audited), typeof(string), typeof(IRequestHandler<Audited, string>));

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(Audited)], [], new StageClosingCache()).Model;

        model.Requests.ShouldHaveSingleItem()
            .Handlers.ShouldHaveSingleItem().ContractType.ShouldBe(typeof(IAuditedHandler<,>));
    }

    [Fact]
    public void Given_A_Void_Handler_Implementing_A_Derived_Contract_When_Capturing_Then_The_Derived_Contract_Is_Recorded()
    {
        HandlerRegistration handler = Handler(
            typeof(AuditedVoidHandler), typeof(AuditedVoid), typeof(NoResult), typeof(IRequestHandler<AuditedVoid>), isVoid: true);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(AuditedVoid)], [], new StageClosingCache()).Model;

        model.Requests.ShouldHaveSingleItem()
            .Handlers.ShouldHaveSingleItem().ContractType.ShouldBe(typeof(IAuditedHandler<>));
    }

    [Fact]
    public void Given_A_Handler_Implementing_Two_Derived_Contracts_When_Capturing_Then_The_Core_Contract_Is_Recorded()
    {
        HandlerRegistration handler = Handler(
            typeof(TwiceDerivedHandler), typeof(TwiceDerived), typeof(string), typeof(IRequestHandler<TwiceDerived, string>));

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(TwiceDerived)], [], new StageClosingCache()).Model;

        model.Requests.ShouldHaveSingleItem()
            .Handlers.ShouldHaveSingleItem().ContractType.ShouldBe(typeof(IRequestHandler<,>));
    }

    [Fact]
    public void Given_A_Stage_Implementing_A_Derived_Contract_When_Capturing_Then_The_Derived_Contract_Is_Recorded()
    {
        HandlerRegistration handler = Handler(
            typeof(PingHandler), typeof(Ping), typeof(string), typeof(IRequestHandler<Ping, string>));
        var declaration = new StageDeclaration(typeof(AuditedStage<,>), handlerFilter: null, StageFamily.Request);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(Ping)], [declaration], new StageClosingCache()).Model;

        model.StageDeclarations.ShouldHaveSingleItem().ContractType.ShouldBe(typeof(IAuditedStage<,>));
        model.Requests.ShouldHaveSingleItem()
            .Stages.ShouldHaveSingleItem().ContractType.ShouldBe(typeof(IAuditedStage<,>));
    }

    // A stage can implement a package contract for one request and only the core contract for
    // another, so each closing records the contract that closing satisfies.
    [Fact]
    public void Given_A_Stage_Deriving_For_One_Request_Only_When_Capturing_Then_Each_Closing_Records_Its_Own_Contract()
    {
        HandlerRegistration first = Handler(
            typeof(FirstHandler), typeof(First), typeof(string), typeof(IRequestHandler<First, string>));
        HandlerRegistration second = Handler(
            typeof(SecondHandler), typeof(Second), typeof(string), typeof(IRequestHandler<Second, string>));
        var declaration = new StageDeclaration(typeof(SplitContractStage), handlerFilter: null, StageFamily.Request);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [first, second], [typeof(First), typeof(Second)], [declaration], new StageClosingCache()).Model;

        model.Requests[0].Stages.ShouldHaveSingleItem().ContractType.ShouldBe(typeof(IAuditedStage<,>));
        model.Requests[1].Stages.ShouldHaveSingleItem().ContractType.ShouldBe(typeof(IRequestStage<,>));
    }

    [Fact]
    public void Given_A_Typed_Stage_Over_A_Void_Request_When_Capturing_Then_The_Closing_Closes_Over_No_Result()
    {
        HandlerRegistration handler = Handler(
            typeof(PurgeHandler), typeof(Purge), typeof(NoResult), typeof(IRequestHandler<Purge>), isVoid: true);
        var declaration = new StageDeclaration(typeof(WrapStage<,>), handlerFilter: null, StageFamily.Request);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(Purge)], [declaration], new StageClosingCache()).Model;

        RequestModel request = model.Requests.ShouldHaveSingleItem();
        request.Handlers.ShouldHaveSingleItem().ResponseType.ShouldBeNull();
        request.Stages.ShouldHaveSingleItem().ClosedType.ShouldBe(typeof(WrapStage<Purge, NoResult>));
    }

    [Theory]
    [InlineData(ServiceLifetime.Transient, RequestFlowLifetime.Transient)]
    [InlineData(ServiceLifetime.Scoped, RequestFlowLifetime.Scoped)]
    [InlineData(ServiceLifetime.Singleton, RequestFlowLifetime.Singleton)]
    public void Given_A_Handler_Registered_With_A_Lifetime_When_Capturing_Then_The_Model_Reports_It(
        ServiceLifetime registered, RequestFlowLifetime expected)
    {
        HandlerRegistration handler = Handler(
            typeof(PingHandler), typeof(Ping), typeof(string), typeof(IRequestHandler<Ping, string>),
            lifetime: registered);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            handlers: [handler], requestTypes: [typeof(Ping)], stageDeclarations: [],
            closings: new StageClosingCache()).Model;

        model.Requests.ShouldHaveSingleItem().Handlers.ShouldHaveSingleItem().Lifetime.ShouldBe(expected);
    }

    // Each AddRequestFlow call decides for the handlers it found, so two handlers can differ.
    [Fact]
    public void Given_Handlers_Registered_With_Different_Lifetimes_When_Capturing_Then_Each_Reports_Its_Own()
    {
        HandlerRegistration scoped = Handler(
            typeof(PingHandler), typeof(Ping), typeof(string), typeof(IRequestHandler<Ping, string>),
            lifetime: ServiceLifetime.Scoped);
        HandlerRegistration transient = Handler(
            typeof(PurgeHandler), typeof(Purge), typeof(NoResult), typeof(IRequestHandler<Purge>), isVoid: true);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            handlers: [scoped, transient], requestTypes: [typeof(Ping), typeof(Purge)], stageDeclarations: [],
            closings: new StageClosingCache()).Model;

        model.Requests[0].Handlers.ShouldHaveSingleItem().Lifetime.ShouldBe(RequestFlowLifetime.Scoped);
        model.Requests[1].Handlers.ShouldHaveSingleItem().Lifetime.ShouldBe(RequestFlowLifetime.Transient);
    }

    [Theory]
    [InlineData(ServiceLifetime.Transient, RequestFlowLifetime.Transient)]
    [InlineData(ServiceLifetime.Scoped, RequestFlowLifetime.Scoped)]
    [InlineData(ServiceLifetime.Singleton, RequestFlowLifetime.Singleton)]
    public void Given_A_Stage_Registered_With_A_Lifetime_When_Capturing_Then_The_Model_Reports_It(
        ServiceLifetime registered, RequestFlowLifetime expected)
    {
        var declaration = new StageDeclaration(typeof(WrapStage<,>), handlerFilter: null, StageFamily.Request, registered);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [], [], [declaration], new StageClosingCache()).Model;

        model.StageDeclarations.ShouldHaveSingleItem().Lifetime.ShouldBe(expected);
    }

    [Fact]
    public void Given_A_Void_Handler_When_Capturing_Then_The_Handler_Reports_Void()
    {
        HandlerRegistration handler = Handler(
            typeof(PurgeHandler), typeof(Purge), typeof(NoResult), typeof(IRequestHandler<Purge>), isVoid: true);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(Purge)], [], new StageClosingCache()).Model;

        HandlerModel captured = model.Requests.ShouldHaveSingleItem().Handlers.ShouldHaveSingleItem();
        captured.IsVoid.ShouldBeTrue();
        captured.ResponseType.ShouldBeNull();
    }

    [Fact]
    public void Given_A_Typed_Handler_When_Capturing_Then_The_Handler_Does_Not_Report_Void()
    {
        HandlerRegistration handler = Handler(
            typeof(PingHandler), typeof(Ping), typeof(string), typeof(IRequestHandler<Ping, string>));

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(Ping)], [], new StageClosingCache()).Model;

        model.Requests.ShouldHaveSingleItem().Handlers.ShouldHaveSingleItem().IsVoid.ShouldBeFalse();
    }

    // A request nothing handles has no chain, so no declaration can claim it.
    [Fact]
    public void Given_A_Request_With_No_Handler_When_Capturing_Then_The_Declaration_Does_Not_Claim_It()
    {
        HandlerRegistration handler = Handler(
            typeof(PingHandler), typeof(Ping), typeof(string), typeof(IRequestHandler<Ping, string>));
        var declaration = new StageDeclaration(typeof(WrapStage<,>), handlerFilter: null, StageFamily.Request);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(Ping), typeof(Purge)], [declaration], new StageClosingCache()).Model;

        model.Requests.Count.ShouldBe(2);
        model.StageDeclarations.ShouldHaveSingleItem().ReachedRequests.ShouldBe([typeof(Ping)]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_One_Stage_Type_With_Only_Task_Reach_When_Capturing_Then_Facts_Keep_Reach_Per_Family(
        bool valueFirst)
    {
        HandlerRegistration handler = Handler(
            typeof(TaskMemoHandler), typeof(TaskMemo), typeof(string), typeof(IRequestHandler<TaskMemo, string>));
        StageDeclaration[] declarations =
        [
            new(typeof(DualMemoStage), handlerFilter: null, StageFamily.Request),
            new(typeof(DualMemoStage), handlerFilter: null, StageFamily.Value),
        ];
        if (valueFirst)
            Array.Reverse(declarations);

        RegistrationSnapshot snapshot = RegistrationSnapshot.Capture(
            [handler], [typeof(TaskMemo)], declarations, new StageClosingCache());

        snapshot.Model.StageDeclarations.ShouldAllBe(declaration => declaration.ReachedRequests.Count == 1);
        snapshot.StageFacts.AnyDeclarationReached(typeof(DualMemoStage), StageFamily.Request, modelFallback: false)
            .ShouldBeTrue();
        snapshot.StageFacts.AnyDeclarationReached(typeof(DualMemoStage), StageFamily.Value, modelFallback: true)
            .ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_No_Stage_Reach_When_Capturing_Then_Completed_Reach_Differs_From_Unavailable_Reach(
        bool hasDeclaration)
    {
        StageDeclaration[] declarations = hasDeclaration
            ? [new(typeof(WrapStage<,>), handlerFilter: null, StageFamily.Request)]
            : [];
        var facts = new StageDeclarationFacts(declarations);

        facts.AnyDeclarationReached(typeof(WrapStage<,>), StageFamily.Request, modelFallback: true)
            .ShouldBeTrue();
        facts.AnyDeclarationReached(typeof(WrapStage<,>), StageFamily.Request, modelFallback: false)
            .ShouldBeFalse();
        StageDeclarationFacts.None.AnyDeclarationReached(
            typeof(WrapStage<,>), StageFamily.Request, modelFallback: true).ShouldBeTrue();

        RegistrationSnapshot snapshot = RegistrationSnapshot.Capture(
            [], [], declarations, new StageClosingCache());

        snapshot.StageFacts.AnyDeclarationReached(typeof(WrapStage<,>), StageFamily.Request, modelFallback: true)
            .ShouldBeFalse();
    }

    [Fact]
    public void Given_Captured_Registrations_When_Inputs_Are_Cleared_And_Captured_Again_Then_The_First_Snapshot_Is_Unchanged()
    {
        HandlerRegistration handler = Handler(
            typeof(PingHandler), typeof(Ping), typeof(string), typeof(IRequestHandler<Ping, string>));
        List<StageDeclaration> declarations =
        [
            new(typeof(WrapStage<,>), handlerFilter: null, StageFamily.Request),
        ];
        var closings = new StageClosingCache();
        RegistrationSnapshot first = RegistrationSnapshot.Capture(
            [handler], [typeof(Ping)], declarations, closings);

        declarations.Clear();
        RegistrationSnapshot second = RegistrationSnapshot.Capture(
            [handler], [typeof(Ping)], declarations, closings);

        first.Model.StageDeclarations.ShouldHaveSingleItem().ReachedRequests.ShouldBe([typeof(Ping)]);
        first.StageFacts.GetFamily(typeof(WrapStage<,>), typeof(IValueRequestStage<,>))
            .ShouldBe(StageFamily.Request);
        first.StageFacts.AnyDeclarationReached(typeof(WrapStage<,>), StageFamily.Request, modelFallback: false)
            .ShouldBeTrue();
        second.Model.StageDeclarations.ShouldBeEmpty();
        second.StageFacts.AnyDeclarationReached(typeof(WrapStage<,>), StageFamily.Request, modelFallback: true)
            .ShouldBeFalse();
    }

    #region Helpers

    private static HandlerRegistration Handler(
        Type handlerType, Type requestType, Type responseType, Type contractType,
        bool isVoid = false, ServiceLifetime lifetime = ServiceLifetime.Transient)
        => new(new HandlerDiscovery(handlerType, requestType, responseType, isVoid, contractType), lifetime);

    public sealed record Ping : IRequest<string>;

    public sealed record Purge : IRequest;

    public sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, CancellationToken cancellationToken)
            => Task.FromResult("pong");
    }

    // Omitting the handler interface keeps assembly scans from finding a duplicate Ping handler.
    public sealed class SecondPingHandler
    { }

    public sealed class PurgeHandler : IRequestHandler<Purge>
    {
        public Task HandleAsync(Purge request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class WrapStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    // A second open generic stage, distinct from WrapStage, so declaration order is
    // distinguishable from duplicate retention.
    public sealed class ExtraStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    public sealed class PurgeOnlyStage : IRequestStage<Purge>
    {
        public Task HandleAsync(Purge request, Continuation next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    // Closes only for a handler returning int, so on MultiPing it reaches the second
    // registration and not the first.
    public sealed class IntResultStage<TRequest> : IRequestStage<TRequest, int>
        where TRequest : IRequest<int>
    {
        public Task<int> HandleAsync(
            TRequest request, Continuation<int> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    // Abstract and unhandled so assembly scans skip this invalid multi-contract request.
    public abstract record MultiPing : IRequest<string>, IRequest<int>;

    // Omitting handler interfaces keeps MultiPing out of assembly scans.
    public sealed class MultiPingStringHandler
    { }

    public sealed class MultiPingIntHandler
    { }

    private sealed record VoidRequest : IRequest;

    private sealed class VoidHandler : IRequestHandler<VoidRequest>
    {
        public Task HandleAsync(VoidRequest request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class VoidStage : IRequestStage<VoidRequest>
    {
        public Task HandleAsync(VoidRequest request, Continuation next, CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private interface IAuditedHandler<in TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    { }

    private interface IAuditedHandler<in TRequest> : IRequestHandler<TRequest>
        where TRequest : IRequest<NoResult>
    { }

    private interface IOtherHandler<in TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    { }

    private interface IAuditedStage<in TRequest, TResponse> : IRequestStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    { }

    private sealed record Audited : IRequest<string>;

    private sealed class AuditedHandler : IAuditedHandler<Audited, string>
    {
        public Task<string> HandleAsync(Audited request, CancellationToken cancellationToken)
            => Task.FromResult(string.Empty);
    }

    private sealed record AuditedVoid : IRequest;

    private sealed class AuditedVoidHandler : IAuditedHandler<AuditedVoid>
    {
        public Task HandleAsync(AuditedVoid request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed record TwiceDerived : IRequest<string>;

    // Neither contract is more derived than the other, so the snapshot falls back to the core one.
    private sealed class TwiceDerivedHandler
        : IAuditedHandler<TwiceDerived, string>, IOtherHandler<TwiceDerived, string>
    {
        public Task<string> HandleAsync(TwiceDerived request, CancellationToken cancellationToken)
            => Task.FromResult(string.Empty);
    }

    private sealed class AuditedStage<TRequest, TResponse> : IAuditedStage<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
            => next.InvokeAsync();
    }

    // Abstract keeps the pair out of whole-assembly scans; the snapshot reads types only.
    private abstract record First : IRequest<string>;

    private abstract record Second : IRequest<string>;

    // Not real IRequestHandler implementers: the builder only reads the Type off a manually built HandlerRegistration.
    private sealed class FirstHandler
    { }

    private sealed class SecondHandler
    { }

    private abstract class SplitContractStage : IAuditedStage<First, string>, IRequestStage<Second, string>
    {
        public abstract Task<string> HandleAsync(
            First request, Continuation<string> next, CancellationToken cancellationToken);

        public abstract Task<string> HandleAsync(
            Second request, Continuation<string> next, CancellationToken cancellationToken);
    }

    private sealed record ValuePing : IValueRequest<string>;

    private sealed class ValuePingHandler : IValueRequestHandler<ValuePing, string>
    {
        public ValueTask<string> HandleAsync(
            ValuePing request,
            CancellationToken cancellationToken)
            => new("pong");
    }

    private sealed record ValueVoid : IValueRequest;

    private sealed class ValueVoidHandler : IValueRequestHandler<ValueVoid>
    {
        public ValueTask HandleAsync(
            ValueVoid request,
            CancellationToken cancellationToken)
            => default;
    }

    private sealed record TaskMemo : IRequest<string>;

    private sealed record ValueMemo : IValueRequest<string>;

    private sealed class TaskMemoHandler : IRequestHandler<TaskMemo, string>
    {
        public Task<string> HandleAsync(
            TaskMemo request,
            CancellationToken cancellationToken)
            => Task.FromResult("task");
    }

    private sealed class ValueMemoHandler : IValueRequestHandler<ValueMemo, string>
    {
        public ValueTask<string> HandleAsync(
            ValueMemo request,
            CancellationToken cancellationToken)
            => new("value");
    }

    private sealed class DualMemoStage
        : IRequestStage<TaskMemo, string>, IValueRequestStage<ValueMemo, string>
    {
        public Task<string> HandleAsync(
            TaskMemo request,
            Continuation<string> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);

        public ValueTask<string> HandleAsync(
            ValueMemo request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed class ValueVoidStage : IValueRequestStage<ValueVoid>
    {
        public ValueTask HandleAsync(
            ValueVoid request,
            ValueContinuation next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private interface IAuditedValueHandler<in TRequest, TResponse>
        : IValueRequestHandler<TRequest, TResponse>
        where TRequest : IValueRequest<TResponse>
    { }

    private sealed record AuditedValue : IValueRequest<string>;

    private sealed class AuditedValueHandler : IAuditedValueHandler<AuditedValue, string>
    {
        public ValueTask<string> HandleAsync(
            AuditedValue request,
            CancellationToken cancellationToken)
            => new("audited");
    }

    private sealed record PublicModelValue : IValueRequest<string>;

    private sealed class PublicModelValueHandler : IValueRequestHandler<PublicModelValue, string>
    {
        public ValueTask<string> HandleAsync(
            PublicModelValue request,
            CancellationToken cancellationToken)
            => new("public");
    }

    private sealed class PublicModelValueStage : IValueRequestStage<PublicModelValue, string>
    {
        public ValueTask<string> HandleAsync(
            PublicModelValue request,
            ValueContinuation<string> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);
    }

    private sealed class ValueContractCaptureRule : IRequestFlowValidationRule
    {
        public static RequestFlowModel? Captured { get; set; }

        public IEnumerable<RequestFlowValidationProblem> Validate(
            RequestFlowValidationContext context)
        {
            Captured = context.Model;
            return [];
        }
    }

    #endregion
}
