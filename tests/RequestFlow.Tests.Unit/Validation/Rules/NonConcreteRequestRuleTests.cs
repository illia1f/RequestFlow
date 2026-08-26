#if NET462
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
#endif
using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class NonConcreteRequestRuleTests
{
#if !NET462
    [Fact]
    public void Given_A_Handler_For_An_Interface_Request_When_Validating_Then_Reports_The_Handler()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(IStringAsk), r => r.AddHandler(typeof(InterfaceAskHandler), typeof(string)))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(ProblemCodes.NonConcreteRequest);
        problem.Subject.ShouldBe(typeof(InterfaceAskHandler));
        problem.Message.ShouldContain("an interface");
    }
#endif

    [Fact]
    public void Given_A_Handler_For_An_Abstract_Request_When_Validating_Then_Reports_The_Handler()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(BaseAsk), r => r.AddHandler(typeof(AbstractAskHandler), typeof(string)))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(ProblemCodes.NonConcreteRequest);
        problem.Subject.ShouldBe(typeof(AbstractAskHandler));
        problem.Message.ShouldContain("an abstract class");
    }

#if NET462
    [Fact]
    public async Task Given_An_Abstract_Marshal_By_Reference_Request_When_Dispatching_A_Transparent_Proxy_Then_Reaches_The_Handler()
    {
        using ServiceProvider provider = Build(o => o.AddHandler<TransparentProxyAskHandler<int>>());
        var request = (TransparentProxyAsk)new TransparentRequestProxy(
            typeof(TransparentProxyAsk)).GetTransparentProxy();

        string response = await provider.GetRequiredService<IRequestDispatcher>()
            .SendAsync(request);

        response.ShouldBe("proxied");
    }

    [Fact]
    public async Task Given_An_Interface_Request_When_Dispatching_A_Transparent_Proxy_Then_Reaches_The_Handler()
    {
        using ServiceProvider provider = Build(o => o.AddHandler<TransparentInterfaceAskHandler<int>>());
        var request = (ITransparentInterfaceAsk)new TransparentRequestProxy(
            typeof(ITransparentInterfaceAsk)).GetTransparentProxy();

        string response = await provider.GetRequiredService<IRequestDispatcher>()
            .SendAsync(request);

        response.ShouldBe("proxied");
    }
#endif

#if !NET462
    [Fact]
    public void Given_A_Void_Handler_For_An_Interface_Request_When_Validating_Then_Reports_The_Handler()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(INoteAsk), r => r.AddHandler(typeof(InterfaceNoteHandler)))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(ProblemCodes.NonConcreteRequest);
        problem.Subject.ShouldBe(typeof(InterfaceNoteHandler));
    }

    [Fact]
    public void Given_A_Stream_Handler_For_An_Interface_Request_When_Validating_Then_Reports_The_Handler()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(
                typeof(ICountAsk),
                r => r.AddHandler(typeof(InterfaceCountHandler), typeof(int), typeof(IStreamRequestHandler<,>)))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(ProblemCodes.NonConcreteRequest);
        problem.Subject.ShouldBe(typeof(InterfaceCountHandler));
    }
#endif

    [Fact]
    public void Given_A_Handler_For_A_Concrete_Request_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(PlainAsk), r => r.AddHandler(typeof(PlainAskHandler), typeof(string)))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    // RF0102 owns the unhandled case, whatever the request type's shape.
    [Fact]
    public void Given_An_Interface_Request_With_No_Handler_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(IStringAsk))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

#if !NET462
    [Fact]
    public void Given_A_Manual_Handler_For_An_Interface_Request_When_Validating_The_Provider_Then_The_Problem_Is_Reported()
    {
        using ServiceProvider provider = Build(o => o.AddHandler<InterfacePingHandler<int>>());

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.ValidateRequestFlow());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(ProblemCodes.NonConcreteRequest);
        problem.Subject.ShouldBe(typeof(InterfacePingHandler<int>));
    }

    [Fact]
    public void Given_A_Manual_Stream_Handler_For_An_Interface_Request_When_Validating_The_Provider_Then_The_Problem_Is_Reported()
    {
        using ServiceProvider provider = Build(o => o.AddHandler<InterfaceCountStreamHandler<int>>());

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.ValidateRequestFlow());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe(ProblemCodes.NonConcreteRequest);
        problem.Subject.ShouldBe(typeof(InterfaceCountStreamHandler<int>));
    }
#endif

    #region Initialization

    private readonly NonConcreteRequestRule _sut = new();

    #endregion

    #region Helpers

    private static ServiceProvider Build(Action<RequestFlowOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(configure);
        return services.BuildServiceProvider();
    }

    private interface IStringAsk : IRequest<string>
    { }

    private abstract record BaseAsk : IRequest<string>;

#if NET462
    public abstract class TransparentProxyAsk : MarshalByRefObject, IRequest<string>
    { }

    public sealed class TransparentProxyAskHandler<T> : IRequestHandler<TransparentProxyAsk, string>
    {
        public Task<string> HandleAsync(
            TransparentProxyAsk request,
            CancellationToken cancellationToken)
            => Task.FromResult("proxied");
    }

    private sealed class TransparentRequestProxy : RealProxy
    {
        private readonly Type _requestType;

        public TransparentRequestProxy(Type requestType)
            : base(requestType)
            => _requestType = requestType;

        public override IMessage Invoke(IMessage message)
        {
            var call = (IMethodCallMessage)message;
            object? value = call.MethodName == "GetType" ? _requestType : null;
            return new ReturnMessage(value, null, 0, null, call);
        }
    }

    public interface ITransparentInterfaceAsk : IRequest<string>
    { }

    public sealed class TransparentInterfaceAskHandler<T>
        : IRequestHandler<ITransparentInterfaceAsk, string>
    {
        public Task<string> HandleAsync(
            ITransparentInterfaceAsk request,
            CancellationToken cancellationToken)
            => Task.FromResult("proxied");
    }
#endif

    private interface INoteAsk : IRequest
    { }

    private interface ICountAsk : IStreamRequest<int>
    { }

    // Abstract keeps these out of the scanner when other tests scan this assembly; the rule reads
    // the model only.
    private abstract class InterfaceAskHandler : IRequestHandler<IStringAsk, string>
    {
        public abstract Task<string> HandleAsync(IStringAsk request, CancellationToken cancellationToken);
    }

    private abstract class AbstractAskHandler : IRequestHandler<BaseAsk, string>
    {
        public abstract Task<string> HandleAsync(BaseAsk request, CancellationToken cancellationToken);
    }

    private abstract class InterfaceNoteHandler : IRequestHandler<INoteAsk>
    {
        public abstract Task HandleAsync(INoteAsk request, CancellationToken cancellationToken);
    }

    private abstract class InterfaceCountHandler : IStreamRequestHandler<ICountAsk, int>
    {
        public abstract IAsyncEnumerable<int> Handle(ICountAsk request, CancellationToken cancellationToken);
    }

    public sealed record PlainAsk : IRequest<string>;

    public sealed class PlainAskHandler : IRequestHandler<PlainAsk, string>
    {
        public Task<string> HandleAsync(PlainAsk request, CancellationToken cancellationToken)
            => Task.FromResult(string.Empty);
    }

    public interface IManualPing : IRequest<string>
    { }

    // Open generic, so the scan skips it; only the manual add in these tests closes it.
    public sealed class InterfacePingHandler<T> : IRequestHandler<IManualPing, string>
    {
        public Task<string> HandleAsync(IManualPing request, CancellationToken cancellationToken)
            => Task.FromResult(string.Empty);
    }

    public interface IManualCount : IStreamRequest<int>
    { }

    // Open generic, so the scan skips it; only the manual add in these tests closes it.
    public sealed class InterfaceCountStreamHandler<T> : IStreamRequestHandler<IManualCount, int>
    {
        public IAsyncEnumerable<int> Handle(IManualCount request, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    #endregion
}
