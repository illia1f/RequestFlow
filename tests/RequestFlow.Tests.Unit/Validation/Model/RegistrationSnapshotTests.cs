using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class RegistrationSnapshotTests
{
    [Fact]
    public void Given_Scanned_Request_Without_Handler_When_Building_Model_Then_Request_Has_No_Handlers()
    {
        RequestFlowModel model = RegistrationSnapshot.Capture(
            handlers: [], requestTypes: [typeof(Ping)], stageDeclarations: [], closings: new StageClosingCache());

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
            handlers: [registration], requestTypes: [], stageDeclarations: [], closings: new StageClosingCache());

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
            handlers: [first, second], requestTypes: [typeof(Ping)], stageDeclarations: [], closings: new StageClosingCache());

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
            closings: new StageClosingCache());

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
            handlers: [], requestTypes: [], stageDeclarations: [first, second], closings: new StageClosingCache());

        model.StageDeclarations.Count.ShouldBe(2);
        model.StageDeclarations[0].StageType.ShouldBe(typeof(WrapStage<,>));
    }

    [Fact]
    public void Given_Scanned_And_Handler_Only_Requests_When_Building_Model_Then_Scanned_Request_Comes_First()
    {
        HandlerRegistration registration = Handler(
            typeof(PingHandler), typeof(Ping), typeof(string), typeof(IRequestHandler<Ping, string>));

        RequestFlowModel model = RegistrationSnapshot.Capture(
            handlers: [registration], requestTypes: [typeof(Purge)], stageDeclarations: [], closings: new StageClosingCache());

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
            closings: new StageClosingCache());

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
            closings: new StageClosingCache());

        ClosedStageModel closing = model.Requests.ShouldHaveSingleItem().Stages.ShouldHaveSingleItem();
        closing.ClosedType.ShouldBe(typeof(IntResultStage<MultiPing>));
    }

    [Fact]
    public void Given_Unhandled_Request_And_A_Stage_Declaration_When_Building_Model_Then_Chain_Is_Empty()
    {
        var declaration = new StageDeclaration(typeof(WrapStage<,>), handlerFilter: null, StageFamily.Request);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            handlers: [], requestTypes: [typeof(Ping)], stageDeclarations: [declaration], closings: new StageClosingCache());

        model.Requests.ShouldHaveSingleItem().Stages.ShouldBeEmpty();
    }

    [Fact]
    public void Given_Distinct_Stage_Declarations_When_Building_Model_Then_Stages_Preserve_Declared_Order()
    {
        var first = new StageDeclaration(typeof(WrapStage<,>), handlerFilter: null, StageFamily.Request);
        var second = new StageDeclaration(typeof(ExtraStage<,>), handlerFilter: null, StageFamily.Request);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            handlers: [], requestTypes: [], stageDeclarations: [first, second], closings: new StageClosingCache());

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
            closings: new StageClosingCache());

        model.Requests.ShouldHaveSingleItem().Stages.ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Void_Stage_Declaration_When_Capturing_Then_The_Void_Contract_Is_Recorded()
    {
        HandlerRegistration handler = Handler(
            typeof(VoidHandler), typeof(VoidRequest), typeof(NoResult), typeof(IRequestHandler<VoidRequest>), isVoid: true);
        var declaration = new StageDeclaration(typeof(VoidStage), handlerFilter: null, StageFamily.Request);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(VoidRequest)], [declaration], new StageClosingCache());

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
            [handler], [typeof(Ping)], [declaration], new StageClosingCache());

        model.StageDeclarations.ShouldHaveSingleItem().ContractType.ShouldBe(typeof(IRequestStage<,>));
        model.Requests.ShouldHaveSingleItem()
            .Stages.ShouldHaveSingleItem().ContractType.ShouldBe(typeof(IRequestStage<,>));
    }

    [Fact]
    public void Given_A_Handler_Implementing_A_Derived_Contract_When_Capturing_Then_The_Derived_Contract_Is_Recorded()
    {
        HandlerRegistration handler = Handler(
            typeof(AuditedHandler), typeof(Audited), typeof(string), typeof(IRequestHandler<Audited, string>));

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(Audited)], [], new StageClosingCache());

        model.Requests.ShouldHaveSingleItem()
            .Handlers.ShouldHaveSingleItem().ContractType.ShouldBe(typeof(IAuditedHandler<,>));
    }

    [Fact]
    public void Given_A_Void_Handler_Implementing_A_Derived_Contract_When_Capturing_Then_The_Derived_Contract_Is_Recorded()
    {
        HandlerRegistration handler = Handler(
            typeof(AuditedVoidHandler), typeof(AuditedVoid), typeof(NoResult), typeof(IRequestHandler<AuditedVoid>), isVoid: true);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(AuditedVoid)], [], new StageClosingCache());

        model.Requests.ShouldHaveSingleItem()
            .Handlers.ShouldHaveSingleItem().ContractType.ShouldBe(typeof(IAuditedHandler<>));
    }

    [Fact]
    public void Given_A_Handler_Implementing_Two_Derived_Contracts_When_Capturing_Then_The_Core_Contract_Is_Recorded()
    {
        HandlerRegistration handler = Handler(
            typeof(TwiceDerivedHandler), typeof(TwiceDerived), typeof(string), typeof(IRequestHandler<TwiceDerived, string>));

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(TwiceDerived)], [], new StageClosingCache());

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
            [handler], [typeof(Ping)], [declaration], new StageClosingCache());

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
            [first, second], [typeof(First), typeof(Second)], [declaration], new StageClosingCache());

        model.Requests[0].Stages.ShouldHaveSingleItem().ContractType.ShouldBe(typeof(IAuditedStage<,>));
        model.Requests[1].Stages.ShouldHaveSingleItem().ContractType.ShouldBe(typeof(IRequestStage<,>));
    }

    // The closed type is what the container resolves, so the void request's chain names NoResult
    // even though the handler reports no response.
    [Fact]
    public void Given_A_Typed_Stage_Over_A_Void_Request_When_Capturing_Then_The_Closing_Closes_Over_No_Result()
    {
        HandlerRegistration handler = Handler(
            typeof(PurgeHandler), typeof(Purge), typeof(NoResult), typeof(IRequestHandler<Purge>), isVoid: true);
        var declaration = new StageDeclaration(typeof(WrapStage<,>), handlerFilter: null, StageFamily.Request);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(Purge)], [declaration], new StageClosingCache());

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
            closings: new StageClosingCache());

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
            closings: new StageClosingCache());

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
            [], [], [declaration], new StageClosingCache());

        model.StageDeclarations.ShouldHaveSingleItem().Lifetime.ShouldBe(expected);
    }

    [Fact]
    public void Given_A_Void_Handler_When_Capturing_Then_The_Handler_Reports_Void()
    {
        HandlerRegistration handler = Handler(
            typeof(PurgeHandler), typeof(Purge), typeof(NoResult), typeof(IRequestHandler<Purge>), isVoid: true);

        RequestFlowModel model = RegistrationSnapshot.Capture(
            [handler], [typeof(Purge)], [], new StageClosingCache());

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
            [handler], [typeof(Ping)], [], new StageClosingCache());

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
            [handler], [typeof(Ping), typeof(Purge)], [declaration], new StageClosingCache());

        model.Requests.Count.ShouldBe(2);
        model.StageDeclarations.ShouldHaveSingleItem().ReachedRequests.ShouldBe([typeof(Ping)]);
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

    // Not a real IRequestHandler<Ping, string> implementer: the builder only reads the Type off
    // a manually built HandlerRegistration, and a second live implementer would make the whole
    // test assembly's scan see a genuine duplicate handler for Ping.
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

    // Closed stage declared for Purge only, so it never satisfies the contract for a Ping handler.
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

    // Implements two closed IRequest<> instantiations so the same request type can carry
    // handler registrations with different response types. Abstract, and with no live handler
    // below, so whole-assembly scans skip it; a scannable multi-contract request would fail
    // every such scan with RF0106.
    public abstract record MultiPing : IRequest<string>, IRequest<int>;

    // Not real IRequestHandler implementers: the builder only reads the Type off a manually
    // built HandlerRegistration, and a live handler would pull MultiPing into every scan.
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

    // Contracts of the kind a package adds on top of the core ones.
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

    // Not real IRequestHandler implementers: the builder only reads the Type off a manually
    // built HandlerRegistration.
    private sealed class FirstHandler
    { }

    private sealed class SecondHandler
    { }

    // Implements the package contract for First and only the core contract for Second.
    private abstract class SplitContractStage : IAuditedStage<First, string>, IRequestStage<Second, string>
    {
        public abstract Task<string> HandleAsync(
            First request, Continuation<string> next, CancellationToken cancellationToken);

        public abstract Task<string> HandleAsync(
            Second request, Continuation<string> next, CancellationToken cancellationToken);
    }

    #endregion
}
