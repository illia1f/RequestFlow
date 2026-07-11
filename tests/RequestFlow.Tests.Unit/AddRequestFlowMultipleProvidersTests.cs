using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class AddRequestFlowMultipleProvidersTests
{
    [Fact]
    public async Task Given_Closing_Declared_After_First_Provider_Resolved_Dispatcher_When_Sending_Request_Via_Second_Provider_Then_Generic_Handler_Handles_Request()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterGenericHandler(typeof(TagHandler<>), typeof(First)));
        using ServiceProvider first = services.BuildServiceProvider();
        _ = first.GetRequiredService<IRequestDispatcher>();
        services.AddRequestFlow(o => o.RegisterGenericHandler(typeof(TagHandler<>), typeof(Second)));
        using ServiceProvider second = services.BuildServiceProvider();
        IRequestDispatcher dispatcher = second.GetRequiredService<IRequestDispatcher>();

        string result = await dispatcher.SendAsync(new Tag<Second>("x"));

        result.ShouldBe("Second:x");
    }

    [Fact]
    public void Given_Closing_Declared_Again_After_First_Provider_Resolved_Dispatcher_When_Registering_Then_Handler_Is_Registered_Once()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterGenericHandler(typeof(TagHandler<>), typeof(First)));
        using ServiceProvider first = services.BuildServiceProvider();
        _ = first.GetRequiredService<IRequestDispatcher>();

        services.AddRequestFlow(o => o.RegisterGenericHandler(typeof(TagHandler<>), typeof(First)));

        services.Count(d => d.ServiceType == typeof(IRequestHandler<Tag<First>, string>)).ShouldBe(1);
    }

    [Fact]
    public void Given_Assembly_Registered_Again_After_First_Provider_Resolved_Dispatcher_When_Registering_Then_Handlers_Are_Registered_Once()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddRequestFlowMultipleProvidersTests>());
        using ServiceProvider first = services.BuildServiceProvider();
        _ = first.GetRequiredService<IRequestDispatcher>();

        services.AddRequestFlow(o => o.RegisterHandlersFromAssemblyContaining<AddRequestFlowMultipleProvidersTests>());

        services.Count(d => d.ServiceType == typeof(IRequestHandler<Ping, string>)).ShouldBe(1);
    }

    #region Helpers

    public sealed record Tag<T>(string Payload) : IRequest<string>;

    public sealed class TagHandler<T> : IRequestHandler<Tag<T>, string>
    {
        public Task<string> HandleAsync(Tag<T> request, CancellationToken cancellationToken)
            => Task.FromResult($"{typeof(T).Name}:{request.Payload}");
    }

    public sealed class First
    { }

    public sealed class Second
    { }

    public sealed record Ping : IRequest<string>;

    public sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, CancellationToken cancellationToken)
            => Task.FromResult("pong");
    }

    #endregion
}
