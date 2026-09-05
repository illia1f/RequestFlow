using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class HandlerScannerTests
{
    [Fact]
    public void Given_Assembly_With_Typed_Handler_When_Scanning_Then_Registration_Captures_Request_And_Response_Types()
    {
        ScanResult result = ScanSelf();

        HandlerDiscovery registration = result.Handlers
            .Where(h => h.ImplementationType == typeof(ScanPingHandler))
            .ShouldHaveSingleItem();
        registration.RequestType.ShouldBe(typeof(ScanPing));
        registration.ResponseType.ShouldBe(typeof(int));
        registration.IsVoid.ShouldBeFalse();
    }

    [Fact]
    public void Given_Assembly_With_Void_Handler_When_Scanning_Then_Registration_Captures_No_Result_Response()
    {
        ScanResult result = ScanSelf();

        HandlerDiscovery registration = result.Handlers
            .Where(h => h.ImplementationType == typeof(ScanVoidHandler))
            .ShouldHaveSingleItem();
        registration.RequestType.ShouldBe(typeof(ScanVoid));
        registration.ResponseType.ShouldBe(typeof(NoResult));
        registration.IsVoid.ShouldBeTrue();
    }

    [Fact]
    public void Given_Assembly_With_Abstract_Handler_When_Scanning_Then_Abstract_Handler_Is_Skipped()
    {
        ScanResult result = ScanSelf();

        result.Handlers.ShouldNotContain(h => h.ImplementationType == typeof(AbstractHandler));
    }

    [Fact]
    public void Given_Assembly_With_Request_Types_When_Scanning_Then_Request_Types_Are_Collected()
    {
        ScanResult result = ScanSelf();

        result.RequestTypes.ShouldContain(typeof(ScanPing));
        result.RequestTypes.ShouldContain(typeof(ScanVoid));
    }

    [Fact]
    public void Given_Assembly_With_Open_Generic_Handler_When_Scanning_Then_Open_Generic_Handler_Is_Skipped()
    {
        ScanResult result = ScanSelf();

        result.Handlers.ShouldNotContain(h => h.ImplementationType.IsGenericTypeDefinition);
    }

    [Fact]
    public void Given_Assembly_With_Open_Generic_Request_When_Scanning_Then_Open_Generic_Request_Is_Skipped()
    {
        ScanResult result = ScanSelf();

        result.RequestTypes.ShouldNotContain(t => t.IsGenericTypeDefinition);
    }

    [Fact]
    public void Given_Assembly_With_Unloadable_Types_When_Scanning_Then_Loadable_Types_Are_Still_Scanned()
    {
        ScanResult result = HandlerScanner.Scan([new PartiallyLoadableAssembly()]);

        result.Handlers.ShouldContain(h => h.ImplementationType == typeof(ScanPingHandler));
        result.RequestTypes.ShouldContain(typeof(ScanPing));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Given_A_Type_Whose_Interfaces_Cannot_Load_When_Scanning_Then_The_Rest_Is_Still_Scanned(
        int failureIndex)
    {
        ScanResult result = HandlerScanner.Scan(
            [new UnloadableInterfaceAssembly(InterfaceFailureAt(failureIndex))]);

        result.Handlers.ShouldContain(h => h.ImplementationType == typeof(ScanPingHandler));
        result.RequestTypes.ShouldContain(typeof(ScanPing));
    }

    [Fact]
    public void Given_A_Typed_Handler_When_Discovering_Then_Records_The_Closed_Core_Contract()
    {
        HandlerDiscovery discovery = HandlerScanner.Discover(typeof(ScanPingHandler)).ShouldHaveSingleItem();

        discovery.Contract.ShouldBe(typeof(IRequestHandler<ScanPing, int>));
        discovery.ContractDefinition.ShouldBe(typeof(IRequestHandler<,>));
    }

    [Fact]
    public void Given_A_Void_Handler_When_Discovering_Then_Records_The_Void_Contract()
    {
        HandlerDiscovery discovery = HandlerScanner.Discover(typeof(ScanVoidHandler)).ShouldHaveSingleItem();

        discovery.Contract.ShouldBe(typeof(IRequestHandler<ScanVoid>));
        discovery.ContractDefinition.ShouldBe(typeof(IRequestHandler<>));
    }

    [Fact]
    public void Given_A_Typed_Value_Handler_When_Discovering_Then_Records_The_Value_Contract()
    {
        HandlerDiscovery typed = HandlerScanner.Discover(typeof(ScanValueHandler)).ShouldHaveSingleItem();

        typed.RequestType.ShouldBe(typeof(ScanValue));
        typed.ResponseType.ShouldBe(typeof(int));
        typed.IsVoid.ShouldBeFalse();
        typed.ContractDefinition.ShouldBe(typeof(IValueRequestHandler<,>));
    }

    [Fact]
    public void Given_A_Plain_Void_Value_Handler_When_Discovering_Then_Records_The_Value_Contract()
    {
        HandlerDiscovery plain = HandlerScanner.Discover(typeof(ScanValueVoidHandler)).ShouldHaveSingleItem();

        plain.RequestType.ShouldBe(typeof(ScanValueVoid));
        plain.ResponseType.ShouldBe(typeof(NoResult));
        plain.IsVoid.ShouldBeTrue();
        plain.ContractDefinition.ShouldBe(typeof(IValueRequestHandler<>));
    }

    [Fact]
    public void Given_Value_Request_Types_When_Scanning_Then_Request_Types_Are_Collected()
    {
        ScanResult result = ScanSelf();

        result.RequestTypes.ShouldContain(typeof(ScanValue));
        result.RequestTypes.ShouldContain(typeof(ScanValueVoid));
    }

    [Fact]
    public void Given_A_Stream_Handler_When_Discovering_Then_Records_The_Item_Type_As_The_Response()
    {
        HandlerDiscovery discovery = HandlerScanner.Discover(typeof(TailHandler)).ShouldHaveSingleItem();

        discovery.RequestType.ShouldBe(typeof(Tail));
        discovery.ResponseType.ShouldBe(typeof(int));
        discovery.IsVoid.ShouldBeFalse();
        discovery.ContractDefinition.ShouldBe(typeof(IStreamRequestHandler<,>));
    }

    [Fact]
    public void Given_A_Stream_Request_When_Scanning_Then_It_Is_Reported_As_A_Request_Type()
    {
        ScanResult result = HandlerScanner.Scan([typeof(HandlerScannerTests).Assembly]);

        result.RequestTypes.ShouldContain(typeof(Tail));
    }

    [Fact]
    public void Given_Concrete_Event_When_Scanning_Then_Event_Is_Known()
    {
        ScanResult result = ScanSelf();

        result.EventTypes.ShouldContain(typeof(ScanEvent));
    }

    [Fact]
    public void Given_Closed_Generic_Event_Handler_When_Scanning_Then_Closed_Subscription_Is_Discovered()
    {
        ScanResult result = ScanSelf();

        result.EventHandlers.ShouldContain(subscription =>
            subscription.HandlerType == typeof(ClosedGenericEventHandler)
            && subscription.DeclaredEventType == typeof(GenericScanEvent<int>));
    }

    [Fact]
    public void Given_Two_Event_Contracts_When_Scanning_Then_Both_Subscriptions_Are_Discovered()
    {
        ScanResult result = ScanSelf();

        EventHandlerDiscovery[] subscriptions = result.EventHandlers
            .Where(subscription => subscription.HandlerType == typeof(TwoEventHandler))
            .ToArray();

        subscriptions.Select(subscription => subscription.DeclaredEventType).ShouldBe([
            typeof(ScanEvent),
            typeof(SecondScanEvent),
        ], ignoreOrder: true);
    }

    [Fact]
    public void Given_Nonconcrete_Event_Types_When_Scanning_Then_They_Are_Not_Known()
    {
        ScanResult result = ScanSelf();

        result.EventTypes.ShouldNotContain(typeof(AbstractScanEvent));
        result.EventTypes.ShouldNotContain(typeof(IScanEvent));
        result.EventTypes.ShouldNotContain(typeof(GenericScanEvent<>));
    }

    [Fact]
    public void Given_An_Open_Generic_Event_Handler_When_Scanning_Then_No_Subscription_Is_Discovered()
    {
        ScanResult result = ScanSelf();

        // An open declared event type reaches MakeGenericMethod at the freeze, so the scan drops it here.
        result.EventHandlers.ShouldNotContain(subscription =>
            subscription.HandlerType == typeof(OpenGenericEventHandler<>)
            || subscription.DeclaredEventType.ContainsGenericParameters);
    }

    [Fact]
    public void Given_A_Scanned_Stream_Handler_When_Registering_Then_It_Resolves_Through_Its_Contract()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<HandlerScannerTests>());
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<IStreamRequestHandler<Tail, int>>().ShouldBeOfType<TailHandler>();
    }

    #region Helpers

    // One case per load failure GetInterfaces can raise for an interface that is not deployed.
    private static Exception InterfaceFailureAt(int index) => index switch
    {
        0 => new TypeLoadException("Could not load type 'Broken' from assembly 'Missing'."),
        1 => new FileNotFoundException("Could not load file or assembly 'Missing'."),
        2 => new FileLoadException("Could not load file or assembly 'Missing'."),
        _ => new BadImageFormatException("Bad IL format in assembly 'Missing'."),
    };

    private static ScanResult ScanSelf()
        => HandlerScanner.Scan([typeof(HandlerScannerTests).Assembly]);

    public sealed record ScanPing : IRequest<int>;

    public sealed record ScanVoid : IRequest;

    public sealed class ScanPingHandler : IRequestHandler<ScanPing, int>
    {
        public Task<int> HandleAsync(ScanPing request, CancellationToken cancellationToken)
            => Task.FromResult(1);
    }

    public sealed class ScanVoidHandler : IRequestHandler<ScanVoid>
    {
        public Task HandleAsync(ScanVoid request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed record ScanValue : IValueRequest<int>;

    public sealed class ScanValueHandler : IValueRequestHandler<ScanValue, int>
    {
        public ValueTask<int> HandleAsync(ScanValue request, CancellationToken cancellationToken)
            => new(1);
    }

    public sealed record ScanValueVoid : IValueRequest;

    public sealed class ScanValueVoidHandler : IValueRequestHandler<ScanValueVoid>
    {
        public ValueTask HandleAsync(ScanValueVoid request, CancellationToken cancellationToken)
            => default;
    }

    public abstract class AbstractHandler : IRequestHandler<ScanPing, int>
    {
        public abstract Task<int> HandleAsync(ScanPing request, CancellationToken cancellationToken);
    }

    public sealed record Tail(int Count) : IStreamRequest<int>;

    public sealed class TailHandler : IStreamRequestHandler<Tail, int>
    {
        public async IAsyncEnumerable<int> Handle(
            Tail request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (int i = 0; i < request.Count; i++)
            {
                await Task.Yield();
                yield return i;
            }
        }
    }

    public sealed record ScanEvent : IEvent;

    public sealed record SecondScanEvent : IEvent;

    public abstract record AbstractScanEvent : IEvent;

    public interface IScanEvent : IEvent;

    public sealed record GenericScanEvent<T> : IEvent;

    public sealed class ClosedGenericEventHandler : IEventHandler<GenericScanEvent<int>>
    {
        public Task HandleAsync(GenericScanEvent<int> @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class TwoEventHandler :
        IEventHandler<ScanEvent>,
        IEventHandler<SecondScanEvent>
    {
        public Task HandleAsync(ScanEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task HandleAsync(SecondScanEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class OpenGenericEventHandler<T> : IEventHandler<GenericScanEvent<T>>
    {
        public Task HandleAsync(GenericScanEvent<T> @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    // Castle cannot proxy Assembly on .NET Framework (ISerializable without a deserialization constructor),
    // so this is a real subclass instead of a substitute.
    private sealed class PartiallyLoadableAssembly : Assembly
    {
        public override Type[] GetTypes()
            => throw new ReflectionTypeLoadException(
                [typeof(ScanPingHandler), typeof(ScanPing), null],
                [new TypeLoadException("Could not load type 'Broken'.")]);
    }

    // A type that loaded while an interface it implements did not; GetTypes reports it, and asking
    // it for that interface throws.
    private sealed class UnloadableInterfaceAssembly(Exception failure) : Assembly
    {
        public override Type[] GetTypes()
            => [new UnloadableInterfaceType(failure), typeof(ScanPingHandler), typeof(ScanPing)];
    }

    private sealed class UnloadableInterfaceType(Exception failure)
        : TypeDelegator(typeof(ScanPingHandler))
    {
        public override Type[] GetInterfaces() => throw failure;
    }

    #endregion
}
