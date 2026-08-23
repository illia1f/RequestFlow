using RequestFlow;

namespace RequestFlow.Tests.Unit.Exceptions;

public sealed class RequestExceptionTests
{
    [Fact]
    public void Given_A_Null_Request_Type_When_Creating_A_Handler_Not_Found_Exception_Then_Throws_Argument_Null_Exception()
    {
        ArgumentNullException exception = Should.Throw<ArgumentNullException>(
            () => new HandlerNotFoundException(null!));

        exception.ParamName.ShouldBe("requestType");
    }
}
