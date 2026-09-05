using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using RequestFlow;

namespace RequestFlow.Tests.Unit.Validation;

public sealed class StreamStageItemMismatchRuleTests
{
    [Fact]
    public void Given_A_Closed_Stage_With_A_Wider_Item_Type_When_Validating_Then_Reports_The_Stage()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringStream))
            .AddStageDeclaration(typeof(WideItemStage), typeof(IStreamRequestStage<,>))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0111");
        problem.Subject.ShouldBe(typeof(WideItemStage));
        problem.Message.ShouldContain("System.Object");
        problem.Message.ShouldContain("System.String");
    }

    [Fact]
    public void Given_A_Closed_Stage_With_The_Declared_Item_Type_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringStream))
            .AddStageDeclaration(typeof(MatchingItemStage), typeof(IStreamRequestStage<,>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Stage_Naming_A_Base_Request_When_A_Derived_Request_Is_Registered_Then_Reports_The_Stage()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(DerivedStream))
            .AddStageDeclaration(typeof(WideItemStage), typeof(IStreamRequestStage<,>))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0111");
        problem.Subject.ShouldBe(typeof(WideItemStage));
    }

    [Fact]
    public void Given_An_Open_Generic_Stage_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringStream))
            .AddStageDeclaration(typeof(OpenStage<,>), typeof(IStreamRequestStage<,>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_An_Open_One_Parameter_Stage_With_A_Wider_Item_Type_When_Validating_Then_Reports_The_Stage()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringStream))
            .AddStageDeclaration(typeof(OpenWideItemStage<>), typeof(IStreamRequestStage<,>))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        RequestFlowValidationProblem problem = problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0111");
        problem.Subject.ShouldBe(typeof(OpenWideItemStage<>));
        problem.Message.ShouldContain("System.Object");
        problem.Message.ShouldContain("System.String");
    }

    [Fact]
    public void Given_Two_Wide_Item_Stages_Over_One_Request_When_Validating_Then_Reports_Both()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringStream))
            .AddStageDeclaration(typeof(WideItemStage), typeof(IStreamRequestStage<,>))
            .AddStageDeclaration(typeof(OpenWideItemStage<>), typeof(IStreamRequestStage<,>))
            .BuildContext();

        List<RequestFlowValidationProblem> problems = [.. _sut.Validate(context)];

        problems.Count.ShouldBe(2);
        problems.ShouldContain(p => p.Subject == typeof(WideItemStage));
        problems.ShouldContain(p => p.Subject == typeof(OpenWideItemStage<>));
    }

    [Fact]
    public void Given_An_Open_One_Parameter_Stage_With_The_Declared_Item_Type_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringStream))
            .AddStageDeclaration(typeof(OpenMatchingItemStage<>), typeof(IStreamRequestStage<,>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_An_Open_One_Parameter_Stage_Whose_Constraints_Exclude_The_Request_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringStream))
            .AddStageDeclaration(typeof(OpenIntItemStage<>), typeof(IStreamRequestStage<,>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Stage_Naming_An_Unregistered_Request_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(OtherStream))
            .AddStageDeclaration(typeof(WideItemStage), typeof(IStreamRequestStage<,>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Task_Stage_Declaration_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StringStream))
            .AddStageDeclaration(typeof(TaskStage))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Request_With_Two_Stream_Contracts_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(TwoStreams))
            .AddStageDeclaration(typeof(TwoStreamsStage), typeof(IStreamRequestStage<,>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    // RF0127 owns a type carrying stream and ValueTask contracts, so there is no sole stream item.
    [Fact]
    public void Given_A_Wide_Stream_Stage_On_A_Stream_And_Value_Request_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(StreamAndValue))
            .AddStageDeclaration(typeof(StreamAndValueStage), typeof(IStreamRequestStage<,>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Wide_Item_Stage_Wrapping_Another_Request_When_Validating_Then_Reports_Nothing()
    {
        RequestFlowValidationContext context = new RequestFlowModelBuilder()
            .AddRequest(typeof(ObjectStream), r => r.AddStage(
                typeof(OpenWideItemStage<>),
                typeof(OpenWideItemStage<ObjectStream>),
                typeof(IStreamRequestStage<,>)))
            .AddRequest(typeof(StringStream))
            .AddStageDeclaration(typeof(OpenWideItemStage<>), typeof(IStreamRequestStage<,>))
            .BuildContext();

        _sut.Validate(context).ShouldBeEmpty();
    }

    [Fact]
    public void Given_A_Filtered_Wide_Item_Stage_Wrapping_A_Marked_Handler_When_Resolving_The_Dispatcher_Then_Nothing_Throws()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StreamStageItemMismatchRuleTests>();
            o.AddStreamStage(typeof(MarkedWideStage<>), s => s.WhereHandlerImplements<IMarked>());
        });

        Should.NotThrow(() => services.BuildServiceProvider().GetRequiredService<IStreamDispatcher>());
    }

    [Fact]
    public void Given_A_Filtered_Wide_Item_Stage_Whose_Filter_Excludes_Every_Handler_When_Resolving_The_Dispatcher_Then_Nothing_Throws()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StreamStageItemMismatchRuleTests>();
            o.AddStreamStage(typeof(MarkedWideStage<>), s => s.WhereHandlerImplements<IUnmatched>());
        });

        Should.NotThrow(() => services.BuildServiceProvider().GetRequiredService<IStreamDispatcher>());
    }

    [Fact]
    public void Given_A_Filtered_Wide_Item_Stage_Whose_Filter_Admits_A_Narrower_Handler_When_Resolving_The_Dispatcher_Then_The_Problem_Is_In_The_Exception()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StreamStageItemMismatchRuleTests>();
            o.AddStreamStage(typeof(MarkedWideStage<>), s => s.WhereHandlerImplements<INarrowMarked>());
        });

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IStreamDispatcher>());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0111");
        problem.Subject.ShouldBe(typeof(MarkedWideStage<>));
    }

    [Fact]
    public void Given_A_Wide_Item_Stream_Stage_When_Resolving_The_Dispatcher_Then_The_Problem_Is_In_The_Exception()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(o =>
        {
            o.RegisterHandlersFromAssemblyContaining<StreamStageItemMismatchRuleTests>();
            o.AddStreamStage<WideNoteStage>();
        });

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IStreamDispatcher>());

        RequestFlowValidationProblem problem = exception.Problems.ShouldHaveSingleItem();
        problem.Code.ShouldBe("RF0111");
        problem.Subject.ShouldBe(typeof(WideNoteStage));
    }

    [Fact]
    public void Given_A_Filtered_And_Unfiltered_Duplicate_Wide_Item_Stage_When_Freezing_Then_Reports_RF0103_And_RF0111()
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options => options
            .AddHandler<NoteHandler>()
            .AddStreamStage<WideNoteStage>(stage => stage.WhereHandlerImplements<IUnmatched>())
            .AddStreamStage<WideNoteStage>());
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.GetRequiredService<IStreamDispatcher>());

        exception.Problems.Count.ShouldBe(2);
        exception.Problems.ShouldContain(problem =>
            problem.Code == ProblemCodes.DuplicateStage
            && problem.Subject == typeof(WideNoteStage));
        exception.Problems.ShouldContain(problem =>
            problem.Code == ProblemCodes.StreamStageItemMismatch
            && problem.Subject == typeof(WideNoteStage));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_A_Dual_Family_Stage_With_A_Stream_Item_Mismatch_When_Freezing_In_Either_Order_Then_Reports_RF0103_And_RF0111(
        bool streamFirst)
    {
        var services = new ServiceCollection();
        services.AddRequestFlow(options =>
        {
            options
                .AddHandler<DualTaskHandler>()
                .AddHandler<DualStreamHandler>();

            if (streamFirst)
            {
                options
                    .AddStreamStage<DualFamilyStage>()
                    .AddStage<DualFamilyStage>();
            }
            else
            {
                options
                    .AddStage<DualFamilyStage>()
                    .AddStreamStage<DualFamilyStage>();
            }
        });
        using ServiceProvider provider = services.BuildServiceProvider();

        RequestFlowValidationException exception = Should.Throw<RequestFlowValidationException>(
            () => provider.GetRequiredService<IStreamDispatcher>());

        exception.Problems.Count.ShouldBe(2);
        exception.Problems.ShouldContain(problem =>
            problem.Code == ProblemCodes.DuplicateStage
            && problem.Subject == typeof(DualFamilyStage));
        exception.Problems.ShouldContain(problem =>
            problem.Code == ProblemCodes.StreamStageItemMismatch
            && problem.Subject == typeof(DualFamilyStage));
    }

    #region Initialization

    private readonly StreamStageItemMismatchRule _sut = new();

    #endregion

    #region Helpers

    // Abstract keeps these out of the scanner when other tests scan this assembly; the rule reads
    // a type's interfaces only.
    private abstract record StringStream : IStreamRequest<string>;

    private abstract record DerivedStream : StringStream;

    private abstract record OtherStream : IStreamRequest<string>;

    private abstract record TwoStreams : IStreamRequest<string>, IStreamRequest<int>;

    private abstract record ObjectStream : IStreamRequest<object>;

    private abstract record StreamAndValue : IStreamRequest<string>, IValueRequest<int>;

    // Compiles because IStreamRequest<TItem> is covariant: StringStream satisfies
    // IStreamRequest<object>, so the stage constraint closes over the wider item.
    private abstract class WideItemStage : IStreamRequestStage<StringStream, object>
    {
        public abstract IAsyncEnumerable<object> Handle(
            StringStream request, StreamContinuation<object> next, CancellationToken cancellationToken);
    }

    private abstract class MatchingItemStage : IStreamRequestStage<StringStream, string>
    {
        public abstract IAsyncEnumerable<string> Handle(
            StringStream request, StreamContinuation<string> next, CancellationToken cancellationToken);
    }

    private abstract class OpenStage<TRequest, TItem> : IStreamRequestStage<TRequest, TItem>
        where TRequest : IStreamRequest<TItem>
    {
        public abstract IAsyncEnumerable<TItem> Handle(
            TRequest request, StreamContinuation<TItem> next, CancellationToken cancellationToken);
    }

    // The one-parameter form of WideItemStage: the class fixes the wider item, and covariance
    // lets the constraint admit requests that declare a narrower one.
    private abstract class OpenWideItemStage<TRequest> : IStreamRequestStage<TRequest, object>
        where TRequest : IStreamRequest<object>
    {
        public abstract IAsyncEnumerable<object> Handle(
            TRequest request, StreamContinuation<object> next, CancellationToken cancellationToken);
    }

    private abstract class OpenMatchingItemStage<TRequest> : IStreamRequestStage<TRequest, string>
        where TRequest : IStreamRequest<string>
    {
        public abstract IAsyncEnumerable<string> Handle(
            TRequest request, StreamContinuation<string> next, CancellationToken cancellationToken);
    }

    // Value-type items get no covariance, so this constraint excludes StringStream outright.
    private abstract class OpenIntItemStage<TRequest> : IStreamRequestStage<TRequest, int>
        where TRequest : IStreamRequest<int>
    {
        public abstract IAsyncEnumerable<int> Handle(
            TRequest request, StreamContinuation<int> next, CancellationToken cancellationToken);
    }

    private abstract class TwoStreamsStage : IStreamRequestStage<TwoStreams, string>
    {
        public abstract IAsyncEnumerable<string> Handle(
            TwoStreams request, StreamContinuation<string> next, CancellationToken cancellationToken);
    }

    private abstract class StreamAndValueStage : IStreamRequestStage<StreamAndValue, object>
    {
        public abstract IAsyncEnumerable<object> Handle(
            StreamAndValue request,
            StreamContinuation<object> next,
            CancellationToken cancellationToken);
    }

    private abstract class TaskStage : IRequestStage<PlainAsk, int>
    {
        public abstract Task<int> HandleAsync(
            PlainAsk request, Continuation<int> next, CancellationToken cancellationToken);
    }

    private abstract record PlainAsk : IRequest<int>;

    public sealed record Note : IStreamRequest<string>;

    public sealed class NoteHandler : IStreamRequestHandler<Note, string>
    {
        public async IAsyncEnumerable<string> Handle(
            Note request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return "note";
        }
    }

    public sealed class WideNoteStage : IStreamRequestStage<Note, object>
    {
        public IAsyncEnumerable<object> Handle(
            Note request, StreamContinuation<object> next, CancellationToken cancellationToken)
            => next.Invoke(cancellationToken);
    }

    public interface IMarked
    { }

    // Deliberately implemented by no handler, so a filter on it leaves the stage wrapping nothing.
    public interface IUnmatched
    { }

    // Marks the one handler whose request declares an item narrower than the wide stage takes.
    public interface INarrowMarked
    { }

    public sealed record Tag : IStreamRequest<string>;

    public sealed class TagHandler : IStreamRequestHandler<Tag, string>, INarrowMarked
    {
        public async IAsyncEnumerable<string> Handle(
            Tag request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return "tag";
        }
    }

    public sealed record Box : IStreamRequest<object>;

    public sealed class BoxHandler : IStreamRequestHandler<Box, object>, IMarked
    {
        public async IAsyncEnumerable<object> Handle(
            Box request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return "box";
        }
    }

    // Covariance admits Note, whose handler the filter excludes, so only Box is wrapped.
    public sealed class MarkedWideStage<TRequest> : IStreamRequestStage<TRequest, object>
        where TRequest : IStreamRequest<object>
    {
        public IAsyncEnumerable<object> Handle(
            TRequest request, StreamContinuation<object> next, CancellationToken cancellationToken)
            => next.Invoke(cancellationToken);
    }

    private sealed record DualTask : IRequest<string>;

    private sealed record DualStream : IStreamRequest<string>;

    private sealed class DualTaskHandler : IRequestHandler<DualTask, string>
    {
        public Task<string> HandleAsync(
            DualTask request,
            CancellationToken cancellationToken)
            => Task.FromResult(string.Empty);
    }

    private sealed class DualStreamHandler : IStreamRequestHandler<DualStream, string>
    {
        public async IAsyncEnumerable<string> Handle(
            DualStream request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return string.Empty;
        }
    }

    private sealed class DualFamilyStage
        : IRequestStage<DualTask, string>, IStreamRequestStage<DualStream, object>
    {
        public Task<string> HandleAsync(
            DualTask request,
            Continuation<string> next,
            CancellationToken cancellationToken)
            => next.InvokeAsync(cancellationToken);

        public IAsyncEnumerable<object> Handle(
            DualStream request,
            StreamContinuation<object> next,
            CancellationToken cancellationToken)
            => next.Invoke(cancellationToken);
    }

    #endregion
}
