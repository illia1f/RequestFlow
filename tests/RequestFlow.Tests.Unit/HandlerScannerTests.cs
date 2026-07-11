using System.Reflection;
using NSubstitute.ExceptionExtensions;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class HandlerScannerTests
{
    [Fact]
    public void Given_Assembly_With_Typed_Handler_When_Scanning_Then_Registration_Captures_Request_And_Response_Types()
    {
        ScanResult result = ScanSelf();

        HandlerRegistration registration = result.Handlers
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

        HandlerRegistration registration = result.Handlers
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
        var assembly = Substitute.For<Assembly>();
        assembly.GetTypes().Throws(new ReflectionTypeLoadException(
            [typeof(ScanPingHandler), typeof(ScanPing), null],
            [new TypeLoadException("Could not load type 'Broken'.")]));

        ScanResult result = HandlerScanner.Scan([assembly]);

        result.Handlers.ShouldContain(h => h.ImplementationType == typeof(ScanPingHandler));
        result.RequestTypes.ShouldContain(typeof(ScanPing));
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

    #endregion
}
