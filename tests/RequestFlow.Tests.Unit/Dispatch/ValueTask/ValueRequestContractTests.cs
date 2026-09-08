using System.Reflection;
using RequestFlow;

namespace RequestFlow.Tests.Unit;

public sealed class ValueRequestContractTests
{
    [Fact]
    public void Given_A_Derived_Response_Request_When_Assigned_To_A_Base_Response_Request_Then_Covariance_Allows_It()
    {
        IValueRequest<Dog> specific = new FetchDog();

        IValueRequest<Animal> widened = specific;

        widened.ShouldBeSameAs(specific);
    }

    [Fact]
    public void Given_The_Void_Handler_Contract_When_Inspecting_Handle_Then_It_Returns_Plain_Value_Task()
    {
        MethodInfo handle = typeof(IValueRequestHandler<Signal>)
            .GetMethod(nameof(IValueRequestHandler<Signal>.HandleAsync))!;

        handle.ReturnType.ShouldBe(typeof(ValueTask));
    }

    [Fact]
    public void Given_The_Value_Dispatcher_When_Inspecting_Its_Overloads_Then_Typed_And_Void_Return_Value_Task_Shapes()
    {
        MethodInfo typed = typeof(IValueRequestDispatcher).GetMethods()
            .Single(method => method.IsGenericMethodDefinition)
            .MakeGenericMethod(typeof(string));
        MethodInfo plain = typeof(IValueRequestDispatcher).GetMethods()
            .Single(method => !method.IsGenericMethod);

        typed.ReturnType.ShouldBe(typeof(ValueTask<string>));
        plain.ReturnType.ShouldBe(typeof(ValueTask));
    }

    [Fact]
    public void Given_The_Value_Stage_Contracts_When_Inspecting_Handle_Then_Typed_And_Void_Return_Value_Task_Shapes()
    {
        MethodInfo typed = typeof(IValueRequestStage<FetchDog, Dog>)
            .GetMethod(nameof(IValueRequestStage<FetchDog, Dog>.HandleAsync))!;
        MethodInfo plain = typeof(IValueRequestStage<Signal>)
            .GetMethod(nameof(IValueRequestStage<Signal>.HandleAsync))!;

        typed.ReturnType.ShouldBe(typeof(ValueTask<Dog>));
        plain.ReturnType.ShouldBe(typeof(ValueTask));
    }

    #region Helpers

    private class Animal
    { }

    private sealed class Dog : Animal
    { }

    private sealed class FetchDog : IValueRequest<Dog>
    { }

    private sealed class FetchDogHandler : IValueRequestHandler<FetchDog, Dog>
    {
        public ValueTask<Dog> HandleAsync(FetchDog request, CancellationToken cancellationToken)
            => new(new Dog());
    }

    private sealed class Signal : IValueRequest
    { }

    private sealed class SignalHandler : IValueRequestHandler<Signal>
    {
        public ValueTask HandleAsync(Signal request, CancellationToken cancellationToken)
            => default;
    }

    #endregion
}
