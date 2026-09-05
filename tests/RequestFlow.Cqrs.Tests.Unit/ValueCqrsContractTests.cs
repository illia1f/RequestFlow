using System.Reflection;

namespace RequestFlow.Cqrs.Tests.Unit;

public sealed class ValueCqrsContractTests
{
    [Fact]
    public void Given_Value_Command_And_Query_Contracts_When_Inspecting_Core_Assignability_Then_They_Are_Value_Requests()
    {
        typeof(IValueRequest<string>).IsAssignableFrom(typeof(IValueCommand<string>)).ShouldBeTrue();
        typeof(IValueRequest<string>).IsAssignableFrom(typeof(IValueQuery<string>)).ShouldBeTrue();
        typeof(IValueCommand).GetInterfaces().ShouldContain(typeof(IValueRequest));
    }

    [Fact]
    public void Given_Value_Command_And_Query_Response_Parameters_When_Inspecting_Variance_Then_They_Are_Covariant()
    {
        GenericParameterAttributes commandVariance = typeof(IValueCommand<>).GetGenericArguments()[0]
            .GenericParameterAttributes & GenericParameterAttributes.VarianceMask;
        GenericParameterAttributes queryVariance = typeof(IValueQuery<>).GetGenericArguments()[0]
            .GenericParameterAttributes & GenericParameterAttributes.VarianceMask;

        commandVariance.ShouldBe(GenericParameterAttributes.Covariant);
        queryVariance.ShouldBe(GenericParameterAttributes.Covariant);
    }

    [Fact]
    public void Given_Value_Cqrs_Handler_Contracts_When_Inspecting_Core_Assignability_Then_They_Are_Value_Request_Handlers()
    {
        typeof(IValueRequestHandler<Rename, string>)
            .IsAssignableFrom(typeof(IValueCommandHandler<Rename, string>))
            .ShouldBeTrue();
        typeof(IValueRequestHandler<Purge>)
            .IsAssignableFrom(typeof(IValueCommandHandler<Purge>))
            .ShouldBeTrue();
        typeof(IValueRequestHandler<FindName, string>)
            .IsAssignableFrom(typeof(IValueQueryHandler<FindName, string>))
            .ShouldBeTrue();
    }

    [Fact]
    public void Given_The_Void_Value_Command_Handler_When_Inspecting_Handle_Then_It_Returns_Plain_Value_Task()
    {
        Type coreHandler = typeof(IValueCommandHandler<Purge>).GetInterfaces()
            .Single(iface => iface.IsGenericType
                && iface.GetGenericTypeDefinition() == typeof(IValueRequestHandler<>));
        MethodInfo handle = coreHandler.GetMethod(nameof(IValueRequestHandler<Purge>.HandleAsync))!;

        handle.ReturnType.ShouldBe(typeof(ValueTask));
    }

    [Fact]
    public void Given_Value_Cqrs_Dispatchers_When_Inspecting_Send_Then_They_Return_Value_Task_Shapes()
    {
        MethodInfo typedCommand = typeof(IValueCommandDispatcher).GetMethods()
            .Single(method => method.IsGenericMethodDefinition)
            .MakeGenericMethod(typeof(string));
        MethodInfo plainCommand = typeof(IValueCommandDispatcher).GetMethods()
            .Single(method => !method.IsGenericMethod);
        MethodInfo query = typeof(IValueQueryDispatcher).GetMethods()
            .Single()
            .MakeGenericMethod(typeof(string));

        typedCommand.ReturnType.ShouldBe(typeof(ValueTask<string>));
        plainCommand.ReturnType.ShouldBe(typeof(ValueTask));
        query.ReturnType.ShouldBe(typeof(ValueTask<string>));
    }

    #region Helpers

    private sealed record Rename : IValueCommand<string>;

    private sealed record Purge : IValueCommand;

    private sealed record FindName : IValueQuery<string>;

    private sealed class RenameHandler : IValueCommandHandler<Rename, string>
    {
        public ValueTask<string> HandleAsync(Rename request, CancellationToken cancellationToken)
            => new("renamed");
    }

    private sealed class PurgeHandler : IValueCommandHandler<Purge>
    {
        public ValueTask HandleAsync(Purge request, CancellationToken cancellationToken)
            => default;
    }

    private sealed class FindNameHandler : IValueQueryHandler<FindName, string>
    {
        public ValueTask<string> HandleAsync(FindName request, CancellationToken cancellationToken)
            => new("name");
    }

    #endregion
}
