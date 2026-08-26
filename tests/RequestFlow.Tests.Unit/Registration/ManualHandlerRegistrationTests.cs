using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class ManualHandlerRegistrationTests
{
    [Fact]
    public async Task Given_A_Handler_Whose_Assembly_Is_Not_Scanned_When_Adding_It_Manually_Then_Dispatch_Reaches_It()
    {
        using ServiceProvider provider = Build(o => o.AddHandler<ManualPingHandler>());

        string response = await provider.GetRequiredService<IRequestDispatcher>()
            .SendAsync(new ManualPing("hi"));

        response.ShouldBe("hi:manual");
    }

    [Fact]
    public async Task Given_A_Void_Handler_When_Adding_It_Manually_Then_Dispatch_Reaches_It()
    {
        using ServiceProvider provider = Build(o => o.AddHandler<ManualNoteHandler>());

        await provider.GetRequiredService<IRequestDispatcher>().SendAsync(new ManualNote());

        ManualNoteHandler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Given_A_Stream_Handler_When_Adding_It_Manually_Then_Stream_Reaches_It()
    {
        using ServiceProvider provider = Build(o => o.AddHandler<ManualCountHandler>());

        List<int> items = [];
        await foreach (int item in provider.GetRequiredService<IStreamDispatcher>()
            .Stream(new ManualCount(3)))
        {
            items.Add(item);
        }

        items.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Given_A_Closed_Generic_Handler_When_Adding_It_Manually_Then_Dispatch_Reaches_It()
    {
        using ServiceProvider provider = Build(o => o.AddHandler<ManualAuditHandler<int>>());

        string response = await provider.GetRequiredService<IRequestDispatcher>()
            .SendAsync(new ManualAudit<int>("p1"));

        response.ShouldBe("Int32:p1");
    }

    [Fact]
    public async Task Given_A_Scanned_Handler_When_Also_Added_Manually_Then_Dispatch_Still_Works()
    {
        using ServiceProvider provider = Build(o => o
            .RegisterHandlersFromAssemblyContaining<ManualHandlerRegistrationTests>()
            .AddHandler<ManualPingHandler>());

        string response = await provider.GetRequiredService<IRequestDispatcher>()
            .SendAsync(new ManualPing("hi"));

        response.ShouldBe("hi:manual");
    }

    [Fact]
    public void Given_A_Scanned_Handler_When_Also_Added_Manually_Then_One_Descriptor_Is_Added()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<ManualHandlerRegistrationTests>()
            .AddHandler<ManualPingHandler>());

        services.Count(descriptor => descriptor.ServiceType == typeof(IRequestHandler<ManualPing, string>))
            .ShouldBe(1);
    }

    [Fact]
    public void Given_The_Same_Manual_Registration_Repeated_When_Registering_Then_One_Registration_Is_Kept()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .AddHandler<ManualPingHandler>()
            .AddHandler<ManualPingHandler>());
        services.AddRequestFlow(o => o.AddHandler<ManualPingHandler>());

        services.Count(descriptor => descriptor.ServiceType == typeof(IRequestHandler<ManualPing, string>))
            .ShouldBe(1);
        RequestFlowRegistry registry = RegistryOf(services);
        registry.Handlers.Count(handler => handler.ImplementationType == typeof(ManualPingHandler))
            .ShouldBe(1);
    }

    [Fact]
    public void Given_Two_Calls_Registering_One_Handler_Under_Different_Lifetimes_When_Registering_Then_The_First_Call_Wins()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o.AddHandler<ManualPingHandler>());
        services.AddRequestFlow(o => o
            .WithScopedHandlers()
            .AddHandler<ManualPingHandler>());

        ServiceDescriptor descriptor = services
            .Single(candidate => candidate.ServiceType == typeof(IRequestHandler<ManualPing, string>));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);
    }

    [Fact]
    public void Given_Scoped_Handlers_Declared_After_The_Manual_Add_When_Registering_Then_The_Handler_Is_Scoped()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .AddHandler<ManualPingHandler>()
            .WithScopedHandlers());

        ServiceDescriptor descriptor = services
            .Single(candidate => candidate.ServiceType == typeof(IRequestHandler<ManualPing, string>));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Given_An_Interface_When_Adding_It_Manually_Then_Freeze_Reports_The_Interface_Problem()
    {
        using ServiceProvider provider = Build(o =>
            o.AddHandler<IRequestHandler<ManualPing, string>>());

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.ValidateRequestFlow());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(ProblemCodes.ManualHandlerIsInterface);
        problem.Subject.ShouldBe(typeof(IRequestHandler<ManualPing, string>));
    }

    [Fact]
    public void Given_An_Abstract_Class_When_Adding_It_Manually_Then_Freeze_Reports_The_Abstract_Problem()
    {
        using ServiceProvider provider = Build(o => o.AddHandler<AbstractManualHandler>());

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.ValidateRequestFlow());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(ProblemCodes.ManualHandlerAbstract);
        problem.Subject.ShouldBe(typeof(AbstractManualHandler));
    }

    [Fact]
    public void Given_A_Class_Without_A_Handler_Contract_When_Adding_It_Manually_Then_Freeze_Reports_The_Missing_Contract_Problem()
    {
        using ServiceProvider provider = Build(o => o.AddHandler<NotARequestHandler>());

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.ValidateRequestFlow());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(ProblemCodes.ManualHandlerMissingContract);
        problem.Subject.ShouldBe(typeof(NotARequestHandler));
    }

    [Fact]
    public void Given_A_Handler_Whose_Interfaces_Cannot_Load_When_Adding_It_Manually_Then_Freeze_Reports_The_Missing_Contract_Problem()
    {
        var handlerType = new UnloadableInterfacesHandlerType();
        using ServiceProvider provider = Build(o => o.ManualHandlers.Add(handlerType));

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.ValidateRequestFlow());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(ProblemCodes.ManualHandlerMissingContract);
        problem.Subject.ShouldBe(handlerType);
    }

    [Fact]
    public void Given_A_Scanned_Handler_When_Excluded_Then_No_Descriptor_Is_Added()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<ManualHandlerRegistrationTests>()
            .ExcludeHandler<SwappablePingHandler>());

        services.ShouldNotContain(descriptor =>
            descriptor.ServiceType == typeof(IRequestHandler<SwappablePing, string>)
            && descriptor.ImplementationType == typeof(SwappablePingHandler));
    }

    [Fact]
    public void Given_A_Scanned_Stream_Handler_When_Excluded_Then_No_Descriptor_Is_Added()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<ManualHandlerRegistrationTests>()
            .ExcludeHandler<ManualCountHandler>());

        services.ShouldNotContain(descriptor =>
            descriptor.ServiceType == typeof(IStreamRequestHandler<ManualCount, int>)
            && descriptor.ImplementationType == typeof(ManualCountHandler));
    }

    [Fact]
    public void Given_The_Only_Handler_Excluded_When_Freezing_Then_The_Request_Is_Unhandled()
    {
        using ServiceProvider provider = Build(o => o
            .RegisterHandlersFromAssemblyContaining<ManualHandlerRegistrationTests>()
            .ExcludeHandler<SwappablePingHandler>());

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.ValidateRequestFlow());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(ProblemCodes.UnhandledRequest);
        problem.Subject.ShouldBe(typeof(SwappablePing));
    }

    [Fact]
    public void Given_The_Only_Handler_Of_An_Unscannable_Request_Excluded_When_Freezing_Then_The_Request_Is_Unhandled()
    {
        using ServiceProvider provider = Build(o => o
            .RegisterHandlersFromAssemblyContaining<ManualHandlerRegistrationTests>()
            .ExcludeHandler<OrphanAuditHandler>());

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.ValidateRequestFlow());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(ProblemCodes.UnhandledRequest);
        problem.Subject.ShouldBe(typeof(OrphanAudit<int>));
    }

    [Fact]
    public async Task Given_An_Excluded_Handler_When_A_Manual_Replacement_Is_Added_Then_Dispatch_Uses_The_Replacement()
    {
        using ServiceProvider provider = Build(o => o
            .RegisterHandlersFromAssemblyContaining<ManualHandlerRegistrationTests>()
            .ExcludeHandler<SwappablePingHandler>()
            .AddHandler<ReplacementPingHandler<SwappablePing>>());

        string response = await provider.GetRequiredService<IRequestDispatcher>()
            .SendAsync(new SwappablePing());

        response.ShouldBe("replacement");
    }

    [Fact]
    public async Task Given_An_Excluded_Handler_When_Also_Added_Manually_Then_Dispatch_Reaches_It()
    {
        using ServiceProvider provider = Build(o => o
            .RegisterHandlersFromAssemblyContaining<ManualHandlerRegistrationTests>()
            .ExcludeHandler<SwappablePingHandler>()
            .AddHandler<SwappablePingHandler>());

        string response = await provider.GetRequiredService<IRequestDispatcher>()
            .SendAsync(new SwappablePing());

        response.ShouldBe("scanned");
    }

    [Fact]
    public void Given_An_Exclusion_In_A_Later_Call_When_Registering_Then_The_Earlier_Scan_Keeps_The_Handler()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o =>
            o.RegisterHandlersFromAssemblyContaining<ManualHandlerRegistrationTests>());
        services.AddRequestFlow(o => o.ExcludeHandler<SwappablePingHandler>());

        services.ShouldContain(descriptor =>
            descriptor.ImplementationType == typeof(SwappablePingHandler));
    }

    [Fact]
    public void Given_An_Exclusion_In_The_First_Scan_When_A_Later_Call_Names_The_Same_Assembly_Then_The_Handler_Stays_Excluded()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<ManualHandlerRegistrationTests>()
            .ExcludeHandler<SwappablePingHandler>());
        services.AddRequestFlow(o =>
            o.RegisterHandlersFromAssemblyContaining<ManualHandlerRegistrationTests>());

        services.ShouldNotContain(descriptor =>
            descriptor.ImplementationType == typeof(SwappablePingHandler));
    }

    [Fact]
    public void Given_A_Dual_Role_Handler_When_Excluded_Then_The_Event_Contract_Stays()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<ManualHandlerRegistrationTests>()
            .ExcludeHandler<DualRoleHandler>()
            .AddHandler<ReplacementPingHandler<DualRolePing>>());

        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(DualRoleHandler));
        services.ShouldNotContain(descriptor =>
            descriptor.ServiceType == typeof(IRequestHandler<DualRolePing, string>)
            && descriptor.ImplementationType == typeof(DualRoleHandler));
    }

    [Fact]
    public void Given_Two_Manual_Handlers_For_One_Request_When_Freezing_Then_Duplicate_Is_Reported()
    {
        using ServiceProvider provider = Build(o => o
            .AddHandler<ManualPingHandler>()
            .AddHandler<ReplacementPingHandler<ManualPing>>());

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.ValidateRequestFlow());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(ProblemCodes.DuplicateHandler);
        problem.Subject.ShouldBe(typeof(ManualPing));
    }

    [Fact]
    public void Given_A_Generic_Closing_When_The_Same_Closed_Type_Is_Also_Added_Manually_Then_One_Registration_Is_Kept()
    {
        var services = new ServiceCollection();

        services.AddRequestFlow(o => o
            .RegisterGenericHandler(typeof(ManualAuditHandler<>), typeof(int))
            .AddHandler<ManualAuditHandler<int>>());

        RequestFlowRegistry registry = RegistryOf(services);
        registry.Handlers.Count(handler => handler.ImplementationType == typeof(ManualAuditHandler<int>))
            .ShouldBe(1);
    }

    #region Helpers

    private static ServiceProvider Build(Action<RequestFlowOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(configure);
        return services.BuildServiceProvider();
    }

    private static RequestFlowRegistry RegistryOf(ServiceCollection services)
        => services
            .Single(descriptor => descriptor.ImplementationInstance is RequestFlowRegistry)
            .ImplementationInstance
            .ShouldBeOfType<RequestFlowRegistry>();

    public sealed record ManualPing(string Text) : IRequest<string>;

    public sealed class ManualPingHandler : IRequestHandler<ManualPing, string>
    {
        public Task<string> HandleAsync(ManualPing request, CancellationToken cancellationToken)
            => Task.FromResult(request.Text + ":manual");
    }

    public sealed record ManualNote : IRequest;

    public sealed class ManualNoteHandler : IRequestHandler<ManualNote>
    {
        public static int Calls;

        public Task HandleAsync(ManualNote request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    public sealed record ManualCount(int Upto) : IStreamRequest<int>;

    public sealed class ManualCountHandler : IStreamRequestHandler<ManualCount, int>
    {
        public async IAsyncEnumerable<int> Handle(
            ManualCount request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (int i = 1; i <= request.Upto; i++)
            {
                yield return i;
                await Task.Yield();
            }
        }
    }

    // Generic request: never a scannable request type, known only through its handler.
    public sealed record ManualAudit<T>(string Payload) : IRequest<string>;

    // Open generic, so the scan skips it; only a manual add can close it.
    public sealed class ManualAuditHandler<T> : IRequestHandler<ManualAudit<T>, string>
    {
        public Task<string> HandleAsync(ManualAudit<T> request, CancellationToken cancellationToken)
            => Task.FromResult($"{typeof(T).Name}:{request.Payload}");
    }

    public abstract class AbstractManualHandler : IRequestHandler<ManualPing, string>
    {
        public Task<string> HandleAsync(ManualPing request, CancellationToken cancellationToken)
            => Task.FromResult(string.Empty);
    }

    public sealed class NotARequestHandler
    { }

    // A handler type that loaded while an interface it implements did not; asking for its interfaces throws.
    private sealed class UnloadableInterfacesHandlerType() : TypeDelegator(typeof(ManualPingHandler))
    {
        public override Type[] GetInterfaces()
            => throw new TypeLoadException("Could not load type 'Contracts.IAudited'.");
    }

    public sealed record SwappablePing : IRequest<string>;

    public sealed class SwappablePingHandler : IRequestHandler<SwappablePing, string>
    {
        public Task<string> HandleAsync(SwappablePing request, CancellationToken cancellationToken)
            => Task.FromResult("scanned");
    }

    // Generic request: never a scannable request type, known to the scan only through its handler.
    public sealed record OrphanAudit<T> : IRequest<string>;

    public sealed class OrphanAuditHandler : IRequestHandler<OrphanAudit<int>, string>
    {
        public Task<string> HandleAsync(OrphanAudit<int> request, CancellationToken cancellationToken)
            => Task.FromResult("orphan");
    }

    // Open generic, so the scan skips it; only a manual add can close it.
    public sealed class ReplacementPingHandler<TRequest> : IRequestHandler<TRequest, string>
        where TRequest : IRequest<string>
    {
        public Task<string> HandleAsync(TRequest request, CancellationToken cancellationToken)
            => Task.FromResult("replacement");
    }

    public sealed record DualRolePing : IRequest<string>;

    public sealed record DualRoleEvent : IEvent;

    public sealed class DualRoleHandler : IRequestHandler<DualRolePing, string>, IEventHandler<DualRoleEvent>
    {
        public Task<string> HandleAsync(DualRolePing request, CancellationToken cancellationToken)
            => Task.FromResult("dual");

        public Task HandleAsync(DualRoleEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    #endregion
}
