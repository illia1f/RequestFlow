using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class ValueHandlerRegistrationTests
{
    [Fact]
    public async Task Given_A_Scanned_Value_Handler_When_Sending_Then_Returns_Its_Response()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
            o.RegisterHandlersFromAssemblyContaining<ValueHandlerRegistrationTests>());
        using ServiceProvider provider = services.BuildServiceProvider();

        string result = await provider.GetRequiredService<IValueRequestDispatcher>()
            .SendAsync(new ScannedValue("ready"));

        result.ShouldBe("ready");
    }

    [Fact]
    public async Task Given_A_Scanned_Plain_Void_Value_Handler_When_Sending_Then_Invokes_The_Handler()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
            o.RegisterHandlersFromAssemblyContaining<ValueHandlerRegistrationTests>());
        using ServiceProvider provider = services.BuildServiceProvider();
        int before = ScannedValueVoidHandler.Calls;

        await provider.GetRequiredService<IValueRequestDispatcher>()
            .SendAsync(new ScannedValueVoid());

        ScannedValueVoidHandler.Calls.ShouldBe(before + 1);
    }

    [Fact]
    public async Task Given_Typed_And_Void_Value_Handlers_When_Added_Manually_Then_Both_Dispatch()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o
            .AddHandler<ManualValueHandler>()
            .AddHandler<ManualValueVoidHandler>());
        using ServiceProvider provider = services.BuildServiceProvider();
        IValueRequestDispatcher dispatcher = provider.GetRequiredService<IValueRequestDispatcher>();
        int before = ManualValueVoidHandler.Calls;

        int result = await dispatcher.SendAsync(new ManualValue(42));
        await dispatcher.SendAsync(new ManualValueVoid());

        result.ShouldBe(42);
        ManualValueVoidHandler.Calls.ShouldBe(before + 1);
    }

    [Fact]
    public async Task Given_An_Excluded_Value_Handler_When_Added_Manually_Then_It_Is_Restored()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<ValueHandlerRegistrationTests>()
            .ExcludeHandler<ExcludedValueHandler>());

        services.ShouldNotContain(descriptor =>
            descriptor.ServiceType == typeof(IValueRequestHandler<ExcludedValue, string>)
            && descriptor.ImplementationType == typeof(ExcludedValueHandler));

        services.AddRequestFlow(o => o.AddHandler<ExcludedValueHandler>());
        using ServiceProvider provider = services.BuildServiceProvider();

        string result = await provider.GetRequiredService<IValueRequestDispatcher>()
            .SendAsync(new ExcludedValue());

        result.ShouldBe("restored");
    }

    [Fact]
    public async Task Given_A_Generic_Typed_Value_Handler_When_Closed_Then_It_Dispatches()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
            o.RegisterGenericHandler(typeof(ValueAuditHandler<>), typeof(Order)));
        using ServiceProvider provider = services.BuildServiceProvider();

        string result = await provider.GetRequiredService<IValueRequestDispatcher>()
            .SendAsync(new ValueAudit<Order>("o1"));

        result.ShouldBe("Order:o1");
    }

    [Fact]
    public async Task Given_A_Generic_Plain_Void_Value_Handler_When_Closed_Then_It_Dispatches()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
            o.RegisterGenericHandler(typeof(ValueNotifyHandler<>), typeof(Order)));
        using ServiceProvider provider = services.BuildServiceProvider();
        int before = ValueNotifyHandler<Order>.Calls;

        await provider.GetRequiredService<IValueRequestDispatcher>()
            .SendAsync(new ValueNotify<Order>());

        ValueNotifyHandler<Order>.Calls.ShouldBe(before + 1);
    }

    [Fact]
    public void Given_Default_Options_When_Registering_A_Value_Handler_Then_Descriptor_Is_Transient()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o =>
            o.RegisterHandlersFromAssemblyContaining<ValueHandlerRegistrationTests>());

        ServiceDescriptor descriptor = services.Single(candidate =>
            candidate.ServiceType == typeof(IValueRequestHandler<ScannedValue, string>));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);
    }

    [Fact]
    public void Given_Scoped_Handlers_When_Registering_A_Value_Handler_Then_Descriptor_Is_Scoped()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<ValueHandlerRegistrationTests>()
            .WithScopedHandlers());

        ServiceDescriptor descriptor = services.Single(candidate =>
            candidate.ServiceType == typeof(IValueRequestHandler<ScannedValue, string>));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Given_Default_Options_When_Registering_Request_Flow_Then_Value_Dispatcher_Is_Scoped()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o =>
            o.RegisterHandlersFromAssemblyContaining<ValueHandlerRegistrationTests>());

        ServiceDescriptor descriptor = services.Single(candidate =>
            candidate.ServiceType == typeof(IValueRequestDispatcher));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Given_Transient_Dispatcher_When_Registering_Request_Flow_Then_Value_Dispatcher_Is_Transient()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<ValueHandlerRegistrationTests>()
            .WithTransientDispatcher());

        ServiceDescriptor descriptor = services.Single(candidate =>
            candidate.ServiceType == typeof(IValueRequestDispatcher));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);
    }

    [Fact]
    public void Given_Repeated_Registration_When_Resolving_Value_Dispatcher_Then_One_Descriptor_And_Map_Are_Used()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
            o.RegisterHandlersFromAssemblyContaining<ValueHandlerRegistrationTests>());
        services.AddRequestFlow(o =>
            o.RegisterHandlersFromAssemblyContaining<ValueHandlerRegistrationTests>());

        services.Count(candidate => candidate.ServiceType == typeof(IValueRequestDispatcher))
            .ShouldBe(1);
        services.Count(candidate => candidate.ServiceType == typeof(FrozenPlans)).ShouldBe(1);
        services.Count(candidate => candidate.ServiceType == typeof(DispatchMap)).ShouldBe(1);

        using ServiceProvider provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IValueRequestDispatcher>();
        FrozenPlans frozen = provider.GetRequiredService<FrozenPlans>();

        provider.GetRequiredService<DispatchMap>().ShouldBeSameAs(frozen.Dispatch);
    }

    #region Helpers

    public sealed record ScannedValue(string Text) : IValueRequest<string>;

    public sealed class ScannedValueHandler : IValueRequestHandler<ScannedValue, string>
    {
        public ValueTask<string> HandleAsync(
            ScannedValue request,
            CancellationToken cancellationToken)
            => new(request.Text);
    }

    public sealed record ScannedValueVoid : IValueRequest;

    public sealed class ScannedValueVoidHandler : IValueRequestHandler<ScannedValueVoid>
    {
        public static int Calls;

        public ValueTask HandleAsync(
            ScannedValueVoid request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return default;
        }
    }

    public sealed record ManualValue(int Number) : IValueRequest<int>;

    public sealed class ManualValueHandler : IValueRequestHandler<ManualValue, int>
    {
        public ValueTask<int> HandleAsync(ManualValue request, CancellationToken cancellationToken)
            => new(request.Number);
    }

    public sealed record ManualValueVoid : IValueRequest;

    public sealed class ManualValueVoidHandler : IValueRequestHandler<ManualValueVoid>
    {
        public static int Calls;

        public ValueTask HandleAsync(ManualValueVoid request, CancellationToken cancellationToken)
        {
            Calls++;
            return default;
        }
    }

    public sealed record ExcludedValue : IValueRequest<string>;

    public sealed class ExcludedValueHandler : IValueRequestHandler<ExcludedValue, string>
    {
        public ValueTask<string> HandleAsync(
            ExcludedValue request,
            CancellationToken cancellationToken)
            => new("restored");
    }

    public sealed record ValueAudit<T>(string Payload) : IValueRequest<string>;

    public sealed class ValueAuditHandler<T> : IValueRequestHandler<ValueAudit<T>, string>
    {
        public ValueTask<string> HandleAsync(
            ValueAudit<T> request,
            CancellationToken cancellationToken)
            => new($"{typeof(T).Name}:{request.Payload}");
    }

    public sealed record ValueNotify<T> : IValueRequest;

    public sealed class ValueNotifyHandler<T> : IValueRequestHandler<ValueNotify<T>>
    {
        public static int Calls;

        public ValueTask HandleAsync(
            ValueNotify<T> request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return default;
        }
    }

    public sealed class Order
    { }

    #endregion
}
