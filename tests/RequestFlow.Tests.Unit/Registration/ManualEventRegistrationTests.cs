using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow.Tests.Unit;

public sealed class ManualEventRegistrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_An_Explicit_Generic_Event_When_Publishing_Then_The_Catch_All_Receives_It(bool useTypeOverload)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.AddEventHandler<CatchAllHandler<int>>();
            if (useTypeOverload)
                o.AddEvent(typeof(EntitySaved<int>));
            else
                o.AddEvent<EntitySaved<int>>();
        });
        var handler = new CatchAllHandler<int>();
        services.AddSingleton(handler);
        using ServiceProvider provider = services.BuildServiceProvider();
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();
        var @event = new EntitySaved<int>(42);

        await publisher.PublishAsync(@event);

        handler.Received.ShouldHaveSingleItem().ShouldBeSameAs(@event);
    }

    [Fact]
    public void Given_An_Explicit_Event_Without_A_Handler_When_Validating_Then_Reports_The_Event()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.AddEvent<EntitySaved<int>>());
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.ValidateRequestFlow());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0114");
        problem.Subject.ShouldBe(typeof(EntitySaved<int>));
    }

    [Theory]
    [InlineData(typeof(IEvent))]
    [InlineData(typeof(AbstractEvent))]
    [InlineData(typeof(EntitySaved<>))]
    [InlineData(typeof(string))]
    public void Given_An_Invalid_Event_Type_When_Validating_Then_Reports_The_Declaration(Type eventType)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o.AddEvent(eventType));
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.ValidateRequestFlow());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0021");
        problem.Subject.ShouldBe(eventType);
        problem.Message.ShouldContain("concrete closed");
    }

    [Fact]
    public void Given_A_Null_Event_Type_When_Registering_Then_Throws_Argument_Null_Exception()
    {
        var options = new RequestFlowOptions();

        ArgumentNullException exception = Should.Throw<ArgumentNullException>(() => options.AddEvent(null!));

        exception.ParamName.ShouldBe("eventType");
    }

    [Fact]
    public async Task Given_A_Scanned_Generic_Event_With_A_Catch_All_When_Publishing_Then_The_Error_Explains_Explicit_Registration()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o => o
            .RegisterHandlersFromAssemblyContaining<ManualEventRegistrationTests>()
            .AddEventHandler<CatchAllHandler<int>>());
        using ServiceProvider provider = services.BuildServiceProvider();
        IEventPublisher publisher = provider.GetRequiredService<IEventPublisher>();

        EventNotRegisteredException exception = await Should.ThrowAsync<EventNotRegisteredException>(
            () => publisher.PublishAsync(new EntitySaved<int>(42)));

        exception.EventType.ShouldBe(typeof(EntitySaved<int>));
        exception.Message.ShouldContain("AddEvent");
        exception.Message.ShouldContain("closed generic");
    }

    #region Helpers

    public sealed record EntitySaved<T>(T Entity) : IEvent;

    public abstract class AbstractEvent : IEvent
    { }

    public sealed class CatchAllHandler<TMarker> : IEventHandler<IEvent>
    {
        public List<IEvent> Received { get; } = [];

        public Task HandleAsync(IEvent @event, CancellationToken cancellationToken)
        {
            Received.Add(@event);
            return Task.CompletedTask;
        }
    }

    #endregion
}
