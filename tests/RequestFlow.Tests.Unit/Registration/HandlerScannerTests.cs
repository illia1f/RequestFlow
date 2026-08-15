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
    public void Given_A_Scanned_Stream_Handler_When_Registering_Then_It_Resolves_Through_Its_Contract()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<HandlerScannerTests>());
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<IStreamRequestHandler<Tail, int>>().ShouldBeOfType<TailHandler>();
    }

    #region Helpers

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

    // Castle cannot proxy Assembly on .NET Framework (ISerializable without a deserialization constructor),
    // so this is a real subclass instead of a substitute.
    private sealed class PartiallyLoadableAssembly : Assembly
    {
        public override Type[] GetTypes()
            => throw new ReflectionTypeLoadException(
                [typeof(ScanPingHandler), typeof(ScanPing), null],
                [new TypeLoadException("Could not load type 'Broken'.")]);
    }

    #endregion
}
