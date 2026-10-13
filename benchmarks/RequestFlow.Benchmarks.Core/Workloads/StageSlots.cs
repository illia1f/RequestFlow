namespace RequestFlow.Benchmarks.Core;

public interface IStageSlot
{
    static abstract int Number { get; }
}

public sealed class Slot1 : IStageSlot
{
    public static int Number => 1;
}

public sealed class Slot2 : IStageSlot
{
    public static int Number => 2;
}

public sealed class Slot3 : IStageSlot
{
    public static int Number => 3;
}

public sealed class Slot4 : IStageSlot
{
    public static int Number => 4;
}

public sealed class Slot5 : IStageSlot
{
    public static int Number => 5;
}

public static class StageSlot<TSlot> where TSlot : IStageSlot
{
    public static readonly string StageName = TraceNames.StagePrefix + TSlot.Number;
    public static readonly string HandlerName = TraceNames.EventHandlerPrefix + TSlot.Number;
    public static readonly string PreHandlerName = "prehandler:" + TSlot.Number;
}
