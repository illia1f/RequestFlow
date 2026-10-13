namespace RequestFlow.Benchmarks.Core;

public static class RuntimeCatalog
{
    /// <summary>
    /// The number of sends in one mixed-traffic invocation.
    /// </summary>
    public const int MixedTrafficBatch = 64;

    private const int MixedTrafficSeed = 20260926;

    private static readonly int[] Sizes = [4, 16, 64];

    public static Type[] Tags(int count)
    {
        if (!Sizes.Contains(count))
            throw new ArgumentOutOfRangeException(nameof(count));

        return AllTags.Take(count).ToArray();
    }

    /// <summary>
    /// Returns the tag of every send in one mixed-traffic invocation over a catalog of <paramref name="count"/> types.
    /// Each tag appears equally often, in an order shuffled once with a fixed seed.
    /// </summary>
    public static IReadOnlyList<Type> MixedTrafficOrder(int count)
        => MixedTrafficOrders.TryGetValue(count, out Type[]? order) ? order : throw new ArgumentOutOfRangeException(nameof(count));

    private static readonly Type[] AllTags =
    [
        typeof(RuntimeTag1),
        typeof(RuntimeTag2),
        typeof(RuntimeTag3),
        typeof(RuntimeTag4),
        typeof(RuntimeTag5),
        typeof(RuntimeTag6),
        typeof(RuntimeTag7),
        typeof(RuntimeTag8),
        typeof(RuntimeTag9),
        typeof(RuntimeTag10),
        typeof(RuntimeTag11),
        typeof(RuntimeTag12),
        typeof(RuntimeTag13),
        typeof(RuntimeTag14),
        typeof(RuntimeTag15),
        typeof(RuntimeTag16),
        typeof(RuntimeTag17),
        typeof(RuntimeTag18),
        typeof(RuntimeTag19),
        typeof(RuntimeTag20),
        typeof(RuntimeTag21),
        typeof(RuntimeTag22),
        typeof(RuntimeTag23),
        typeof(RuntimeTag24),
        typeof(RuntimeTag25),
        typeof(RuntimeTag26),
        typeof(RuntimeTag27),
        typeof(RuntimeTag28),
        typeof(RuntimeTag29),
        typeof(RuntimeTag30),
        typeof(RuntimeTag31),
        typeof(RuntimeTag32),
        typeof(RuntimeTag33),
        typeof(RuntimeTag34),
        typeof(RuntimeTag35),
        typeof(RuntimeTag36),
        typeof(RuntimeTag37),
        typeof(RuntimeTag38),
        typeof(RuntimeTag39),
        typeof(RuntimeTag40),
        typeof(RuntimeTag41),
        typeof(RuntimeTag42),
        typeof(RuntimeTag43),
        typeof(RuntimeTag44),
        typeof(RuntimeTag45),
        typeof(RuntimeTag46),
        typeof(RuntimeTag47),
        typeof(RuntimeTag48),
        typeof(RuntimeTag49),
        typeof(RuntimeTag50),
        typeof(RuntimeTag51),
        typeof(RuntimeTag52),
        typeof(RuntimeTag53),
        typeof(RuntimeTag54),
        typeof(RuntimeTag55),
        typeof(RuntimeTag56),
        typeof(RuntimeTag57),
        typeof(RuntimeTag58),
        typeof(RuntimeTag59),
        typeof(RuntimeTag60),
        typeof(RuntimeTag61),
        typeof(RuntimeTag62),
        typeof(RuntimeTag63),
        typeof(RuntimeTag64),
    ];

    private static readonly Dictionary<int, Type[]> MixedTrafficOrders = Sizes.ToDictionary(size => size, CreateMixedTrafficOrder);

    private static Type[] CreateMixedTrafficOrder(int count)
    {
        Type[] order = [.. Enumerable.Repeat(Tags(count), MixedTrafficBatch / count).SelectMany(tags => tags)];

        new Random(MixedTrafficSeed).Shuffle(order);

        return order;
    }
}

public sealed class RuntimeTag1;
public sealed class RuntimeTag2;
public sealed class RuntimeTag3;
public sealed class RuntimeTag4;
public sealed class RuntimeTag5;
public sealed class RuntimeTag6;
public sealed class RuntimeTag7;
public sealed class RuntimeTag8;
public sealed class RuntimeTag9;
public sealed class RuntimeTag10;
public sealed class RuntimeTag11;
public sealed class RuntimeTag12;
public sealed class RuntimeTag13;
public sealed class RuntimeTag14;
public sealed class RuntimeTag15;
public sealed class RuntimeTag16;
public sealed class RuntimeTag17;
public sealed class RuntimeTag18;
public sealed class RuntimeTag19;
public sealed class RuntimeTag20;
public sealed class RuntimeTag21;
public sealed class RuntimeTag22;
public sealed class RuntimeTag23;
public sealed class RuntimeTag24;
public sealed class RuntimeTag25;
public sealed class RuntimeTag26;
public sealed class RuntimeTag27;
public sealed class RuntimeTag28;
public sealed class RuntimeTag29;
public sealed class RuntimeTag30;
public sealed class RuntimeTag31;
public sealed class RuntimeTag32;
public sealed class RuntimeTag33;
public sealed class RuntimeTag34;
public sealed class RuntimeTag35;
public sealed class RuntimeTag36;
public sealed class RuntimeTag37;
public sealed class RuntimeTag38;
public sealed class RuntimeTag39;
public sealed class RuntimeTag40;
public sealed class RuntimeTag41;
public sealed class RuntimeTag42;
public sealed class RuntimeTag43;
public sealed class RuntimeTag44;
public sealed class RuntimeTag45;
public sealed class RuntimeTag46;
public sealed class RuntimeTag47;
public sealed class RuntimeTag48;
public sealed class RuntimeTag49;
public sealed class RuntimeTag50;
public sealed class RuntimeTag51;
public sealed class RuntimeTag52;
public sealed class RuntimeTag53;
public sealed class RuntimeTag54;
public sealed class RuntimeTag55;
public sealed class RuntimeTag56;
public sealed class RuntimeTag57;
public sealed class RuntimeTag58;
public sealed class RuntimeTag59;
public sealed class RuntimeTag60;
public sealed class RuntimeTag61;
public sealed class RuntimeTag62;
public sealed class RuntimeTag63;
public sealed class RuntimeTag64;
