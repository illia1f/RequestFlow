using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Events;

public sealed class EventStrategyOptionsTests
{
    [Fact]
    public void Given_A_Global_Custom_Strategy_When_Declaring_Then_Defaults_To_Singleton()
    {
        var sut = new RequestFlowOptions();

        sut.PublishAllEventsWith<TestStrategy>();

        EventStrategyDeclaration declaration = sut.EventStrategyDeclarations.ShouldHaveSingleItem();
        declaration.DeclaredEventType.ShouldBeNull();
        declaration.StrategyType.ShouldBe(typeof(TestStrategy));
        declaration.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void Given_A_Per_Event_Custom_Strategy_When_Declaring_Then_Records_The_Event_Contract()
    {
        var sut = new RequestFlowOptions();

        sut.PublishEventsWith<ITestEvent, TestStrategy>(options => options.AsScoped());

        EventStrategyDeclaration declaration = sut.EventStrategyDeclarations.ShouldHaveSingleItem();
        declaration.DeclaredEventType.ShouldBe(typeof(ITestEvent));
        declaration.StrategyType.ShouldBe(typeof(TestStrategy));
        declaration.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Given_A_Transient_Custom_Strategy_When_Declaring_Then_Records_Transient()
    {
        var sut = new RequestFlowOptions();

        sut.PublishAllEventsWith<TestStrategy>(options => options.AsTransient());

        sut.EventStrategyDeclarations.ShouldHaveSingleItem().Lifetime
            .ShouldBe(ServiceLifetime.Transient);
    }

    [Fact]
    public void Given_The_Same_Lifetime_Twice_When_Configuring_Then_It_Is_Accepted()
    {
        var sut = new RequestFlowOptions();

        Should.NotThrow(() => sut.PublishAllEventsWith<TestStrategy>(options =>
        {
            options.AsScoped();
            options.AsScoped();
        }));
    }

    [Fact]
    public void Given_Two_Different_Lifetimes_When_Configuring_Then_Throws_Invalid_Operation_Exception()
    {
        var sut = new RequestFlowOptions();

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            sut.PublishAllEventsWith<TestStrategy>(options =>
            {
                options.AsScoped();
                options.AsTransient();
            }));

        exception.Message.ShouldContain("Scoped");
        exception.Message.ShouldContain("Transient");
    }

    [Fact]
    public void Given_A_Built_In_Strategy_When_Configuring_A_Lifetime_Then_Throws_Invalid_Operation_Exception()
    {
        var sut = new RequestFlowOptions();
        Action[] declarations =
        [
            () => sut.PublishAllEventsWith<SequentialPublishStrategy>(options => options.AsScoped()),
            () => sut.PublishAllEventsWith<ParallelPublishStrategy>(options => options.AsTransient()),
            () => sut.PublishAllEventsWith<FailFastPublishStrategy>(options => options.AsSingleton()),
        ];

        foreach (Action declare in declarations)
        {
            InvalidOperationException exception = Should.Throw<InvalidOperationException>(declare);
            exception.Message.ShouldContain("built-in");
        }
    }

    [Fact]
    public void Given_Parallel_Publication_Sugar_When_Declaring_Then_Records_The_Parallel_Built_In()
    {
        var sut = new RequestFlowOptions();

        sut.PublishEventsInParallel();

        EventStrategyDeclaration declaration = sut.EventStrategyDeclarations.ShouldHaveSingleItem();
        declaration.DeclaredEventType.ShouldBeNull();
        declaration.StrategyType.ShouldBe(typeof(ParallelPublishStrategy));
        declaration.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void Given_Sequential_Publication_Sugar_When_Declaring_Then_Records_The_Sequential_Built_In()
    {
        var sut = new RequestFlowOptions();

        sut.PublishEventsSequentially();

        EventStrategyDeclaration declaration = sut.EventStrategyDeclarations.ShouldHaveSingleItem();
        declaration.DeclaredEventType.ShouldBeNull();
        declaration.StrategyType.ShouldBe(typeof(SequentialPublishStrategy));
        declaration.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void Given_Fail_Fast_Publication_Sugar_When_Declaring_Then_Records_The_Fail_Fast_Built_In()
    {
        var sut = new RequestFlowOptions();

        sut.PublishEventsFailFast();

        EventStrategyDeclaration declaration = sut.EventStrategyDeclarations.ShouldHaveSingleItem();
        declaration.DeclaredEventType.ShouldBeNull();
        declaration.StrategyType.ShouldBe(typeof(FailFastPublishStrategy));
        declaration.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void Given_Per_Event_Parallel_Sugar_When_Declaring_Then_Records_The_Event_Contract()
    {
        var sut = new RequestFlowOptions();

        sut.PublishEventsInParallel<ITestEvent>();

        EventStrategyDeclaration declaration = sut.EventStrategyDeclarations.ShouldHaveSingleItem();
        declaration.DeclaredEventType.ShouldBe(typeof(ITestEvent));
        declaration.StrategyType.ShouldBe(typeof(ParallelPublishStrategy));
    }

    [Fact]
    public void Given_Per_Event_Sequential_Sugar_When_Declaring_Then_Records_The_Event_Contract()
    {
        var sut = new RequestFlowOptions();

        sut.PublishEventsSequentially<ITestEvent>();

        EventStrategyDeclaration declaration = sut.EventStrategyDeclarations.ShouldHaveSingleItem();
        declaration.DeclaredEventType.ShouldBe(typeof(ITestEvent));
        declaration.StrategyType.ShouldBe(typeof(SequentialPublishStrategy));
    }

    [Fact]
    public void Given_Per_Event_Fail_Fast_Sugar_When_Declaring_Then_Records_The_Event_Contract()
    {
        var sut = new RequestFlowOptions();

        sut.PublishEventsFailFast<ITestEvent>();

        EventStrategyDeclaration declaration = sut.EventStrategyDeclarations.ShouldHaveSingleItem();
        declaration.DeclaredEventType.ShouldBe(typeof(ITestEvent));
        declaration.StrategyType.ShouldBe(typeof(FailFastPublishStrategy));
    }

    #region Helpers

    private interface ITestEvent : IEvent
    { }

    private sealed class TestStrategy : IEventPublishStrategy
    {
        public Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    #endregion
}
