using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow.Tests.Unit;

public sealed class ValidationReentryTests
{
    [Theory]
    [InlineData(typeof(IRequestDispatcher), false, false)]
    [InlineData(typeof(IValueRequestDispatcher), false, false)]
    [InlineData(typeof(IStreamDispatcher), false, false)]
    [InlineData(typeof(IEventPublisher), false, false)]
    [InlineData(typeof(IRequestDispatcher), false, true)]
    [InlineData(typeof(IValueRequestDispatcher), false, true)]
    [InlineData(typeof(IStreamDispatcher), false, true)]
    [InlineData(typeof(IEventPublisher), false, true)]
    [InlineData(typeof(IRequestDispatcher), true, false)]
    [InlineData(typeof(IValueRequestDispatcher), true, false)]
    [InlineData(typeof(IStreamDispatcher), true, false)]
    [InlineData(typeof(IEventPublisher), true, false)]
    public async Task Given_A_Rule_With_A_Dispatch_Dependency_When_Validating_Then_Explains_The_Cycle(
        Type dependencyType, bool transient, bool resolveDispatcher)
    {
        await Completes(() =>
        {
            var services = new ServiceCollection();
            services.AddRequestFlow(o =>
            {
                if (transient)
                    o.WithTransientDispatcher();
            });
            services.AddSingleton(typeof(IRequestFlowValidationRule),
                typeof(DependentRule<>).MakeGenericType(dependencyType));
            using ServiceProvider provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = transient });

            InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            {
                if (resolveDispatcher)
                    provider.GetRequiredService(dependencyType);
                else
                    provider.ValidateRequestFlow();
            });

            exception.Message.ShouldContain("validation");
            exception.Message.ShouldContain("dispatcher");
            exception.Message.ShouldContain("IEventPublisher");
        });
    }

    [Fact]
    public async Task Given_A_Dependency_Cycle_On_The_First_Attempt_When_Validation_Is_Retried_Then_It_Can_Succeed()
    {
        await Completes(() =>
        {
            var services = new ServiceCollection();
            services.AddRequestFlow(_ => { });
            int attempts = 0;
            services.AddSingleton<IRequestFlowValidationRule>(sp =>
            {
                if (attempts++ == 0)
                    sp.GetRequiredService<IRequestDispatcher>();
                return new PassingRule();
            });
            using ServiceProvider provider = services.BuildServiceProvider();

            Should.Throw<InvalidOperationException>(() => provider.ValidateRequestFlow());

            Should.NotThrow(() => provider.ValidateRequestFlow());
            attempts.ShouldBe(2);
        });
    }

    [Fact]
    public async Task Given_Two_Providers_Sharing_A_Registry_When_One_Validates_The_Other_Then_Both_Can_Freeze()
    {
        await Completes(() =>
        {
            var services = new ServiceCollection();
            services.AddRequestFlow(_ => { });
            ServiceProvider? secondProvider = null;
            int attempts = 0;
            services.AddSingleton<IRequestFlowValidationRule>(_ =>
            {
                if (attempts++ == 0)
                    secondProvider!.ValidateRequestFlow();
                return new PassingRule();
            });
            using ServiceProvider firstProvider = services.BuildServiceProvider();
            using (secondProvider = services.BuildServiceProvider())
            {
                Should.NotThrow(() => firstProvider.ValidateRequestFlow());

                attempts.ShouldBe(2);
                firstProvider.GetRequiredService<FrozenPlans>()
                    .ShouldNotBeSameAs(secondProvider.GetRequiredService<FrozenPlans>());
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Rule_Reentering_Validation_When_It_Runs_Then_The_Failure_Is_Reported(bool inspect)
    {
        await Completes(() =>
        {
            var services = new ServiceCollection();
            services.AddRequestFlow(_ => { });
            services.AddSingleton<IRequestFlowValidationRule>(sp => new ReenteringRule(sp, inspect));
            using ServiceProvider provider = services.BuildServiceProvider();

            RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
                () => provider.ValidateRequestFlow());

            RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
            problem.Code.ShouldBe("RF0107");
            problem.Message.ShouldContain("validation");
            problem.Message.ShouldContain("dependency");
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_A_Context_Captured_During_Validation_When_Validating_After_It_Ends_Then_It_Can_Succeed(
        bool failFirst)
    {
        await Completes(() =>
        {
            var services = new ServiceCollection();
            services.AddRequestFlow(_ => { });
            ExecutionContext? captured = null;
            int attempts = 0;
            services.AddSingleton<IRequestFlowValidationRule>(new CallbackRule(() =>
            {
                if (attempts++ == 0)
                {
                    captured = ExecutionContext.Capture();
                    if (failFirst)
                        throw new InvalidOperationException("The first validation attempt fails.");
                }
            }));
            using ServiceProvider provider = services.BuildServiceProvider();
            if (failFirst)
                Should.Throw<RequestFlowValidationException>(() => provider.ValidateRequestFlow());
            else
                provider.ValidateRequestFlow();

            Should.NotThrow(() => ExecutionContext.Run(captured!, _ => provider.ValidateRequestFlow(), null));

            attempts.ShouldBe(failFirst ? 2 : 1);
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Given_A_Rule_Waiting_For_Worker_Validation_When_It_Runs_Then_The_Cycle_Is_Reported(
        bool inspect, bool useScope)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(_ => { });
        Task? worker = null;
        int attempts = 0;
        services.AddSingleton<IRequestFlowValidationRule>(sp => new CallbackRule(() =>
        {
            if (Interlocked.Increment(ref attempts) != 1)
                return;

            worker = Task.Factory.StartNew(() =>
            {
                using IServiceScope? scope = useScope ? sp.CreateScope() : null;
                IServiceProvider target = scope?.ServiceProvider ?? sp;
                if (inspect)
                    target.InspectRequestFlow(typeof(object));
                else
                    target.ValidateRequestFlow();
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            if (!((IAsyncResult)worker).AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2)))
                throw new TimeoutException("The validation worker is blocked by the active freeze.");
            worker.GetAwaiter().GetResult();
        }));
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.ValidateRequestFlow());
        await Record.ExceptionAsync(() => worker!);

        exception.Problems.ShouldHaveSingleItem().Code.ShouldBe("RF0107");
        Exception cause = exception.InnerException.ShouldBeOfType<AggregateException>()
            .InnerExceptions.ShouldHaveSingleItem();
        cause.ShouldBeOfType<InvalidOperationException>();
        cause.Message.ShouldContain("context.Model");
    }

    [Theory]
    [InlineData(typeof(IRequestDispatcher))]
    [InlineData(typeof(IValueRequestDispatcher))]
    [InlineData(typeof(IStreamDispatcher))]
    [InlineData(typeof(IEventPublisher))]
    public async Task Given_A_Rule_Resolving_A_Dispatch_Surface_When_It_Validates_Then_RF0107_Preserves_The_Cause(
        Type dependencyType)
    {
        await Completes(() =>
        {
            var services = new ServiceCollection();
            services.AddRequestFlow(_ => { });
            Exception? cause = null;
            services.AddSingleton<IRequestFlowValidationRule>(sp => new CallbackRule(() =>
            {
                try
                {
                    sp.GetRequiredService(dependencyType);
                }
                catch (Exception exception)
                {
                    cause = exception;
                    throw;
                }
            }));
            using ServiceProvider provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = false });

            RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
                () => provider.ValidateRequestFlow());

            exception.Problems.ShouldHaveSingleItem().Code.ShouldBe("RF0107");
            cause.ShouldBeOfType<InvalidOperationException>();
            exception.InnerException.ShouldBeOfType<AggregateException>()
                .InnerExceptions.ShouldHaveSingleItem().ShouldBeSameAs(cause);
        });
    }

    [Fact]
    public async Task Given_An_Independent_Context_When_Another_Pass_Is_Running_Then_The_Guard_Allows_Validation()
    {
        using var validating = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var services = new ServiceCollection();
        services.AddRequestFlow(_ => { });
        int attempts = 0;
        services.AddSingleton<IRequestFlowValidationRule>(new CallbackRule(() =>
        {
            Interlocked.Increment(ref attempts);
            validating.Set();
            if (!release.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("The test did not release the validation pass.");
        }));
        using ServiceProvider provider = services.BuildServiceProvider();

        Task first = Task.Factory.StartNew(() => provider.ValidateRequestFlow(),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            validating.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();

            Should.NotThrow(() => provider.GetRequiredService<RequestFlowRegistry>().ThrowIfFreezing(provider));
        }
        finally
        {
            release.Set();
            await first;
        }

        Should.NotThrow(() => provider.ValidateRequestFlow());
        attempts.ShouldBe(1);
    }

    #region Helpers

    private static async Task Completes(Action action)
    {
        Task validation = Task.Run(action);
        Task completed = await Task.WhenAny(validation, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.ShouldBeSameAs(validation, "validation must report the dependency cycle instead of hanging startup");
        await validation;
    }

    private sealed class PassingRule : IRequestFlowValidationRule
    {
        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
            => [];
    }

    private sealed class CallbackRule(Action action) : IRequestFlowValidationRule
    {
        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
        {
            action();
            return [];
        }
    }

    private sealed class ReenteringRule(IServiceProvider provider, bool inspect) : IRequestFlowValidationRule
    {
        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
        {
            if (inspect)
                provider.InspectRequestFlow(typeof(object));
            else
                provider.ValidateRequestFlow();
            return [];
        }
    }

    private sealed class DependentRule<TDependency>(TDependency dependency) : IRequestFlowValidationRule
    {
        public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
        {
            GC.KeepAlive(dependency);
            return [];
        }
    }

    #endregion
}
