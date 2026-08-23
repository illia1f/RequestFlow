using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class EventStrategyValidationIntegrationTests
{
    [Fact]
    public void Given_An_Interface_Strategy_When_Freezing_Then_Reports_RF0013()
    {
        RequestFlowValidationException exception = Freeze(options =>
            options.PublishAllEventsWith<IInvalidStrategy>());

        Problem(exception, ProblemCodes.EventStrategyIsInterface)
            .Subject.ShouldBe(typeof(IInvalidStrategy));
    }

    [Fact]
    public void Given_An_Abstract_Strategy_When_Freezing_Then_Reports_RF0014()
    {
        RequestFlowValidationException exception = Freeze(options =>
            options.PublishAllEventsWith<AbstractStrategy>());

        Problem(exception, ProblemCodes.EventStrategyAbstract)
            .Subject.ShouldBe(typeof(AbstractStrategy));
    }

    [Fact]
    public void Given_Parallel_And_Fail_Fast_Global_Declarations_When_Freezing_Then_Reports_RF0119()
    {
        RequestFlowValidationException exception = Freeze(
            options => options.PublishEventsInParallel(),
            options => options.PublishAllEventsWith<FailFastPublishStrategy>());

        Problem(exception, ProblemCodes.ConflictingEventStrategies)
            .Subject.ShouldBeNull();
    }

    [Fact]
    public void Given_Unrelated_Interface_Declarations_When_Freezing_Then_Reports_RF0120()
    {
        RequestFlowValidationException exception = Freeze(options => options
            .PublishEventsWith<IFirstEvent, StrategyA>()
            .PublishEventsWith<ISecondEvent, StrategyB>());

        Problem(exception, ProblemCodes.AmbiguousEventStrategy)
            .Subject.ShouldBe(typeof(AmbiguousEvent));
    }

    [Fact]
    public void Given_One_Strategy_With_Two_Lifetimes_When_Freezing_Then_Reports_RF0121()
    {
        RequestFlowValidationException exception = Freeze(
            options => options.PublishAllEventsWith<StrategyA>(),
            options => options.PublishEventsWith<AmbiguousEvent, StrategyA>(
                strategy => strategy.AsScoped()));

        Problem(exception, ProblemCodes.EventStrategyLifetime)
            .Subject.ShouldBe(typeof(StrategyA));
    }

    [Fact]
    public void Given_An_Unused_Strategy_Declaration_When_Freezing_Then_Reports_RF0122()
    {
        RequestFlowValidationException exception = Freeze(options => options
            .DisallowUnusedEventHandlers()
            .PublishEventsWith<IUnusedEvent, StrategyA>());

        Problem(exception, ProblemCodes.UnusedEventStrategy)
            .Subject.ShouldBe(typeof(IUnusedEvent));
    }

    [Fact]
    public void Given_A_Strategy_And_Event_Handler_With_Different_Lifetimes_When_Freezing_Then_Reports_RF0123()
    {
        RequestFlowValidationException exception = Freeze(options =>
            options.PublishAllEventsWith<DualRoleStrategy>());

        Problem(exception, ProblemCodes.EventStrategyRoleLifetime)
            .Subject.ShouldBe(typeof(DualRoleStrategy));
    }

    [Fact]
    public void Given_A_Strategy_And_Event_Handler_With_The_Same_Lifetime_When_Freezing_Then_Reports_Nothing()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options => options
            .RegisterHandlersFromAssemblyContaining<EventStrategyValidationIntegrationTests>()
            .AllowUnhandledRequests()
            .PublishAllEventsWith<DualRoleStrategy>(strategy => strategy.AsTransient()));
        using ServiceProvider provider = services.BuildServiceProvider();

        Should.NotThrow(() => provider.ValidateRequestFlow());
    }

    [Fact]
    public void Given_A_Closed_Strategy_Also_Registered_Through_An_Open_Stage_With_A_Different_Lifetime_When_Freezing_Then_Reports_RF0123()
    {
        RequestFlowValidationException exception = Freeze(options => options
            .AddStage(typeof(OpenStageStrategy<,>))
            .PublishAllEventsWith<OpenStageStrategy<StageRequest, string>>());

        Problem(exception, ProblemCodes.EventStrategyRoleLifetime)
            .Subject.ShouldBe(typeof(OpenStageStrategy<StageRequest, string>));
    }

    #region Helpers

    private static RequestFlowValidationException Freeze(
        Action<RequestFlowOptions> configure,
        Action<RequestFlowOptions>? configureSecond = null)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options =>
        {
            options.RegisterHandlersFromAssemblyContaining<
                EventStrategyValidationIntegrationTests>();
            options.AllowUnhandledRequests();
            configure(options);
        });
        if (configureSecond is not null)
            services.AddRequestFlow(configureSecond);

        using ServiceProvider provider = services.BuildServiceProvider();
        return Should.Throw<RequestFlowValidationException>(
            () => provider.ValidateRequestFlow());
    }

    private static RequestFlowValidationProblem Problem(
        RequestFlowValidationException exception,
        string code)
        => exception.Problems.Single(problem => problem.Code == code);

    public interface IFirstEvent : IEvent;

    public interface ISecondEvent : IEvent;

    public interface IUnusedEvent : IEvent;

    public sealed record AmbiguousEvent : IFirstEvent, ISecondEvent;

    public sealed class AmbiguousEventHandler : IEventHandler<AmbiguousEvent>
    {
        public Task HandleAsync(
            AmbiguousEvent @event,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public interface IInvalidStrategy : IEventPublishStrategy;

    public abstract class AbstractStrategy : IEventPublishStrategy
    {
        public Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed class StrategyA : AbstractStrategy;

    public sealed class StrategyB : AbstractStrategy;

    public sealed record DualRoleEvent : IEvent;

    public sealed class DualRoleStrategy : IEventPublishStrategy, IEventHandler<DualRoleEvent>
    {
        public Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task HandleAsync(DualRoleEvent @event, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public sealed record StageRequest : IRequest<string>;

    public sealed class StageRequestHandler : IRequestHandler<StageRequest, string>
    {
        public Task<string> HandleAsync(
            StageRequest request,
            CancellationToken cancellationToken)
            => Task.FromResult(string.Empty);
    }

    public sealed class OpenStageStrategy<TRequest, TResponse>
        : IRequestStage<TRequest, TResponse>, IEventPublishStrategy
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(
            TRequest request,
            Continuation<TResponse> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);

        public Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    #endregion
}
