using System.Reflection;
using BenchmarkDotNet.Attributes;
using RequestFlow.Benchmarks.Harness;

namespace RequestFlow.Benchmarks.Core;

public static class BenchmarkVerification
{
    /// <summary>
    /// Checks the result and trace of a benchmark call, which passes <see cref="CancellationToken.None"/>.
    /// </summary>
    public static void VerifyWorkload(
        BenchmarkObservation observation, string[] expectedNames, int? expectedResult = null, bool ordered = true)
    {
        string label = Label(observation);

        if (expectedResult is not null && !Equals(observation.Result, expectedResult.Value))
            throw new InvalidOperationException($"{label} returned {observation.Result ?? "no result"}, expected {expectedResult}.");

        TraceAssertions.VerifyRecords(label, observation.Trace ?? [], expectedNames, CancellationToken.None, ordered);
    }

    public static string[] RequestNames(int stages = 0, string prefix = TraceNames.StagePrefix)
        => [.. Enumerable.Range(1, stages).Select(index => prefix + index), TraceNames.Handler];

    /// <summary>
    /// Returns the stream trace names. An early stop reads one item and records no completion.
    /// </summary>
    public static string[] StreamNames(int stages, int count, bool early = false)
        => [.. RequestNames(stages), .. Enumerable.Range(0, early ? 1 : count).Select(index => TraceNames.ItemPrefix + index),
            .. early ? Array.Empty<string>() : new[] { TraceNames.StreamComplete }, TraceNames.StreamDispose];

    public static string[] EventNames(int handlers)
        => Enumerable.Range(1, handlers)
            .SelectMany(index => new[]
            {
                TraceNames.EventHandlerPrefix + index,
                TraceNames.EventHandlerPrefix + index + TraceNames.CompletionSuffix,
            })
            .ToArray();

    public static void VerifyHandlerTypes(BenchmarkObservation observation, Type[] expectedTypes)
    {
        Type[] actual = (observation.Trace ?? [])
            .Where(entry => entry.Name == TraceNames.Handler || TraceNames.IsEventHandlerStart(entry.Name))
            .Select(entry => entry.Component.GetType()).ToArray();

        if (!actual.SequenceEqual(expectedTypes))
        {
            string expectedTypeNames = string.Join(", ", expectedTypes.Select(type => type.ToString()));
            string actualTypeNames = string.Join(", ", actual.Select(type => type.ToString()));

            throw new InvalidOperationException(
                $"Benchmark '{observation.BenchmarkType.Name}.{observation.Method}' executed the wrong handler types. " +
                $"Expected: {expectedTypeNames}. Actual: {actualTypeNames}.");
        }
    }

    /// <summary>
    /// Returns the values of a <see cref="ParamsAttribute"/> on a benchmark property or field.
    /// </summary>
    public static T[] Params<T>(Type benchmarkType, string member)
    {
        MemberInfo declaration = benchmarkType.GetMember(member, BindingFlags.Public | BindingFlags.Instance).SingleOrDefault()
            ?? throw new InvalidOperationException($"'{benchmarkType.Name}' has no member '{member}'.");
        ParamsAttribute attribute = declaration.GetCustomAttribute<ParamsAttribute>()
            ?? throw new InvalidOperationException($"'{benchmarkType.Name}.{member}' has no [Params] attribute.");

        return attribute.Values.Cast<T>().ToArray();
    }

    /// <summary>
    /// Runs every benchmark method of every parameter set twice and checks it against its class expectation.
    /// </summary>
    /// <remarks>
    /// Every class declares its scope lifetime with <see cref="BenchmarkScopeAttribute"/>.
    /// A <see cref="ScopeLifetime.ScopePerOperation"/> class must use a new scope per call and dispose it before the call completes.
    /// A <see cref="ScopeLifetime.RetainedScope"/> class must retain one scope across calls.
    /// A <see cref="ScopeLifetime.DirectHandler"/> class is exempt from scope checks.
    /// A <c>Completion</c> parameter fixes whether the returned awaitable is complete when the call returns.
    /// </remarks>
    public static BenchmarkProbeResult Run(
        Assembly assembly,
        IReadOnlyDictionary<Type, Func<IReadOnlyDictionary<string, object?>, BenchmarkExpectation>> expectations)
        => Run(
            assembly.GetTypes().Where(type => type.IsPublic && !type.IsAbstract &&
                FindMethodsWithAttribute(type, typeof(BenchmarkAttribute)).Length > 0).ToArray(),
            expectations);

    internal static BenchmarkProbeResult Run(
        Type[] benchmarkTypes,
        IReadOnlyDictionary<Type, Func<IReadOnlyDictionary<string, object?>, BenchmarkExpectation>> expectations)
    {
        foreach (Type type in benchmarkTypes)
        {
            if (!expectations.ContainsKey(type))
                throw new InvalidOperationException($"Benchmark '{type.Name}' has no workload expectations.");

            // Throws when the class declares no scope lifetime.
            ScopeVerification.For(type);
        }

        int parameterSets = 0;
        int cases = 0;

        foreach (Type type in benchmarkTypes)
        {
            MethodInfo[] methods = FindMethodsWithAttribute(type, typeof(BenchmarkAttribute));

            VerifyBaselines(type, methods);

            if (methods.Select(method => method.GetCustomAttribute<BenchmarkAttribute>()!.OperationsPerInvoke).Distinct().Count() != 1)
                throw new InvalidOperationException($"'{type.Name}' must use one OperationsPerInvoke for every method.");

            foreach (Dictionary<string, object?> parameters in EnumerateParameterSets(type))
            {
                VerifyParameterSet(type, methods, parameters, expectations[type](parameters));

                parameterSets++;
                cases += methods.Length;
            }
        }

        if (cases == 0)
            throw new InvalidOperationException("No benchmark methods were verified.");

        return new BenchmarkProbeResult(benchmarkTypes.Length, parameterSets, cases);
    }

    /// <summary>
    /// Requires one baseline per <see cref="BenchmarkCategoryAttribute"/> group. Methods without a category form one group.
    /// </summary>
    internal static void VerifyBaselines(Type type, MethodInfo[] methods)
    {
        foreach (IGrouping<string, MethodInfo> group in methods.GroupBy(CategoryKey, StringComparer.Ordinal))
        {
            if (group.Count(method => method.GetCustomAttribute<BenchmarkAttribute>()!.Baseline) != 1)
            {
                string category = group.Key.Length == 0 ? "its uncategorized methods" : $"category '{group.Key}'";

                throw new InvalidOperationException($"'{type.Name}' must declare one baseline for {category}.");
            }
        }
    }

    private static string CategoryKey(MethodInfo method)
        => string.Join(",", method.GetCustomAttributes<BenchmarkCategoryAttribute>(true)
            .SelectMany(attribute => attribute.Categories).Distinct().Order(StringComparer.Ordinal));

    // BenchmarkDotNet gives every benchmark method its own instance and setup, so the probe does too.
    private static void VerifyParameterSet(
        Type type,
        MethodInfo[] methods,
        Dictionary<string, object?> parameters,
        BenchmarkExpectation expectation)
    {
        foreach (MethodInfo method in methods)
        {
            object instance = CreateInstance(type, parameters);

            try
            {
                InvokeLifecycleMethod(instance, method, typeof(GlobalSetupAttribute));

                BenchmarkObservation first = ObserveMethod(instance, method, parameters, expectation);
                BenchmarkObservation second = ObserveMethod(instance, method, parameters, expectation);

                ScopeVerification.VerifyAcrossCalls(first, second, expectation);

                expectation.Verify(first);
                expectation.Verify(second);
            }
            catch (Exception exception)
            {
                string parameterDescription = string.Join(", ", parameters.Select(pair => pair.Key + "=" + pair.Value));
                Exception cause = exception is TargetInvocationException invocation
                    ? invocation.InnerException ?? exception
                    : exception;

                throw new InvalidOperationException(
                    $"Benchmark correctness verification failed for '{type.Name}.{method.Name}' ({parameterDescription}).", cause);
            }
            finally
            {
                InvokeLifecycleMethod(instance, method, typeof(GlobalCleanupAttribute));
            }
        }
    }

    private static object CreateInstance(Type type, IReadOnlyDictionary<string, object?> parameters)
    {
        object instance = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Cannot construct '{type.FullName}'.");

        foreach ((string name, object? value) in parameters)
        {
            if (type.GetProperty(name) is PropertyInfo property)
                property.SetValue(instance, value);
            else
                type.GetField(name)!.SetValue(instance, value);
        }

        return instance;
    }

    // Matches BenchmarkDotNet: a lifecycle method that targets the benchmark wins over an untargeted one.
    private static void InvokeLifecycleMethod(object instance, MethodInfo benchmark, Type attribute)
    {
        MethodInfo? lifecycle = FindMethodsWithAttribute(instance.GetType(), attribute)
            .Select(method => (Method: method, Attribute: (TargetedAttribute)method.GetCustomAttribute(attribute, true)!))
            .OrderByDescending(candidate => candidate.Attribute.Targets.Length)
            .FirstOrDefault(candidate => candidate.Attribute.Match(benchmark))
            .Method;

        if (lifecycle is not null)
            WaitForResult(lifecycle.Invoke(instance, null));
    }

    private static BenchmarkObservation ObserveMethod(
        object instance, MethodInfo method, IReadOnlyDictionary<string, object?> parameters, BenchmarkExpectation expectation)
    {
        Type type = instance.GetType();
        var trace = new ExecutionTrace(expectation.ParallelHandlers);
        // A field holds one message or an array of them.
        var messages = InstanceFields(type)
            .Select(field => field.GetValue(instance))
            .SelectMany(value => value as object?[] ?? [value])
            .OfType<WorkloadMessage>()
            .Distinct()
            .Select(message => (Message: message, OriginalTrace: message.Trace))
            .ToArray();

        try
        {
            foreach (var message in messages)
                message.Message.Trace = trace;

            object? operation = null;
            object? result;
            bool? completedOnReturn = null;

            try
            {
                using (var continuations = new DeferredContinuations())
                {
                    operation = method.Invoke(instance, null);
                    completedOnReturn = IsCompleted(operation);

                    if (continuations.DeadlineExpired)
                        throw new InvalidOperationException($"'{type.Name}.{method.Name}' blocked on its own continuation.");
                }

                trace.VerifyFanOut();
            }
            finally
            {
                trace.ReleaseHandlers();
                result = WaitForResult(operation);
            }

            trace.VerifyCompletion();

            var observation = new BenchmarkObservation(type, method.Name, parameters, result, trace.Entries);

            VerifyCompletionShape(observation, completedOnReturn);
            ScopeVerification.VerifyAfterCall(observation, expectation);

            return observation;
        }
        finally
        {
            foreach (var message in messages)
                message.Message.Trace = message.OriginalTrace;
        }
    }

    private static void VerifyCompletionShape(BenchmarkObservation observation, bool? completedOnReturn)
    {
        bool? synchronous = observation.Parameters.TryGetValue("Completion", out object? completion) && completion is CompletionKind kind
            ? kind != CompletionKind.Suspending
            : null;

        if (synchronous is null)
            return;

        if (completedOnReturn is null)
            throw new InvalidOperationException($"{Label(observation)} returns no awaitable, so its completion shape cannot be checked.");

        if (completedOnReturn != synchronous)
        {
            string expected = synchronous.Value ? "complete synchronously" : "suspend";

            throw new InvalidOperationException($"{Label(observation)} did not {expected}.");
        }
    }

    private static bool? IsCompleted(object? operation)
        => operation?.GetType().GetProperty(nameof(Task.IsCompleted))?.GetValue(operation) as bool?;

    internal static string Label(BenchmarkObservation observation)
    {
        string parameterDescription = string.Join(
            ", ",
            observation.Parameters.Select(pair => pair.Key + "=" + pair.Value));

        return $"Benchmark '{observation.BenchmarkType.Name}.{observation.Method}' ({parameterDescription})";
    }

    // Abstract benchmark bases declare private fields that GetFields does not return for the derived type.
    private static IEnumerable<FieldInfo> InstanceFields(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                yield return field;
        }
    }

    private static MethodInfo[] FindMethodsWithAttribute(Type type, Type attribute)
        => type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.IsDefined(attribute, true)).ToArray();

    private static IEnumerable<Dictionary<string, object?>> EnumerateParameterSets(Type type)
    {
        MemberInfo[] members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Where(member => member.GetCustomAttribute<ParamsAttribute>() is not null).ToArray();
        IEnumerable<Dictionary<string, object?>> sets = [new Dictionary<string, object?>()];

        foreach (MemberInfo member in members)
        {
            object?[] values = member.GetCustomAttribute<ParamsAttribute>()!.Values;
            sets = sets.SelectMany(previous => values.Select(value =>
            {
                var next = new Dictionary<string, object?>(previous) { [member.Name] = value };

                return next;
            })).ToArray();
        }

        return sets;
    }

    private static object? WaitForResult(object? result)
    {
        if (result is ValueTask valueTask)
        {
            valueTask.AsTask().GetAwaiter().GetResult();

            return null;
        }

        if (result is not null && result.GetType().IsGenericType &&
            result.GetType().GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            result = result.GetType().GetMethod("AsTask")!.Invoke(result, null);
        }

        if (result is Task task)
        {
            task.GetAwaiter().GetResult();

            return task.GetType().GetProperty("Result")?.GetValue(task);
        }

        return result;
    }
}
