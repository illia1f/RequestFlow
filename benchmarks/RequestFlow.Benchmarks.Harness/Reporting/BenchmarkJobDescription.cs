using BenchmarkDotNet.Characteristics;
using BenchmarkDotNet.Jobs;

namespace RequestFlow.Benchmarks.Harness;

internal sealed record BenchmarkJobDescription(
    string Id,
    string Description,
    IReadOnlyDictionary<string, string> Characteristics,
    string[] Benchmarks)
{
    internal static BenchmarkJobDescription Create(Job job)
    {
        var characteristics = job.GetCharacteristicsWithValues()
            .Where(characteristic => !characteristic.HasChildCharacteristics &&
                characteristic != CharacteristicObject.IdCharacteristic)
            .OrderBy(characteristic => characteristic.DeclaringType.Name, StringComparer.Ordinal)
            .ThenBy(characteristic => characteristic.Id, StringComparer.Ordinal)
            .ToDictionary(characteristic => $"{characteristic.DeclaringType.Name}.{characteristic.Id}",
                characteristic => CharacteristicPresenter.DefaultPresenter.ToPresentation(job, characteristic));

        string description = string.Join(", ", characteristics.Select(pair => $"{pair.Key}={pair.Value}"));

        return new BenchmarkJobDescription(job.ResolvedId, description, characteristics, []);
    }
}
