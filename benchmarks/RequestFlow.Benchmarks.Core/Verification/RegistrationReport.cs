using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow.Benchmarks.Core;

/// <summary>
/// Lists and filters the DI descriptors the benchmark fixtures register.
/// </summary>
public static class RegistrationReport
{
    /// <summary>
    /// Prints workload components, the dispatcher, <paramref name="reportedServices"/>, and implementations of <paramref name="reportedContracts"/>.
    /// </summary>
    /// <param name="reportedServices">Service types to print; an open generic type matches every closed registration of it.</param>
    /// <param name="reportedContracts">Open generic interfaces whose implementations to print, whatever service type registers them.</param>
    public static void ReportRegistrations(
        string label,
        IServiceCollection registrations,
        Type dispatcherType,
        IReadOnlyCollection<Type>? reportedServices = null,
        IReadOnlyCollection<Type>? reportedContracts = null)
    {
        Console.WriteLine(label);

        Type[] services = [typeof(ScopeWitness), dispatcherType, .. reportedServices ?? []];
        IReadOnlyCollection<Type> contracts = reportedContracts ?? [];

        foreach (ServiceDescriptor descriptor in registrations.Where(descriptor =>
            IsComponent(descriptor) || Implements(descriptor, contracts) ||
            services.Any(service => Matches(descriptor.ServiceType, service))))
        {
            string implementation = GetImplementationType(descriptor)?.ToString() ??
                (descriptor.IsKeyedService ? descriptor.KeyedImplementationInstance : descriptor.ImplementationInstance)?.GetType().ToString() ??
                "factory";
            object? serviceKey = descriptor.IsKeyedService ? descriptor.ServiceKey : "none";

            Console.WriteLine($"  {descriptor.ServiceType}: {descriptor.Lifetime}; implementation={implementation}; key={serviceKey}");
        }
    }

    /// <summary>
    /// Returns registrations for workload components.
    /// </summary>
    public static IEnumerable<ServiceDescriptor> GetComponentDescriptors(IServiceCollection services)
        => services.Where(IsComponent);

    internal static Type? GetImplementationType(ServiceDescriptor descriptor)
        => descriptor.IsKeyedService ? descriptor.KeyedImplementationType : descriptor.ImplementationType;

    private static bool IsComponent(ServiceDescriptor descriptor)
        => GetImplementationType(descriptor) is Type implementation &&
            typeof(WorkloadComponent).IsAssignableFrom(implementation);

    private static bool Implements(ServiceDescriptor descriptor, IReadOnlyCollection<Type> contracts)
    {
        Type type = GetImplementationType(descriptor) ?? descriptor.ServiceType;

        return type.GetInterfaces().Any(contract =>
            contract.IsGenericType && contracts.Contains(contract.GetGenericTypeDefinition()));
    }

    private static bool Matches(Type serviceType, Type reported)
        => serviceType == reported ||
            (reported.IsGenericTypeDefinition && serviceType.IsGenericType &&
                serviceType.GetGenericTypeDefinition() == reported);
}
