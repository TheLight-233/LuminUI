using LuminUI.Testing;
using Xunit;

namespace LuminUI.Tests;

public sealed class ReactiveAllocationTests
{
    private sealed class ValueSink
    {
        public int Last;
        public void Set(int value) => Last = value;
    }

    private sealed class CollectionSink
    {
        public int Last;
        public void Added(int index, int value) => Last = value;
        public void Removed(int index, int value) => Last = value;
        public void Replaced(int index, int oldValue, int newValue) => Last = newValue;
        public void Moved(int oldIndex, int newIndex, int value) => Last = value;
        public void Cleared() => Last = -1;
    }

    private sealed class DictionarySink
    {
        public int Last;
        public void Added(int key, int value) => Last = value;
        public void Removed(int key, int value) => Last = value;
        public void Replaced(int key, int oldValue, int newValue) => Last = newValue;
        public void Cleared() => Last = -1;
    }

    [Fact]
    public void ReactiveProperty_SteadyStateNotification_AllocatesZeroBytes()
    {
        var property = new ReactiveProperty<int>();
        var sink = new ValueSink();
        property.SubscribeNoPush(sink.Set);
        property.Value = 1; // JIT + comparer warmup

        Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 2; i < 100_002; i++) property.Value = i;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Equal(100_001, sink.Last);
    }

    [Fact]
    public void ReactiveCollection_SteadyStateReplace_AllocatesZeroBytes()
    {
        var collection = new ReactiveCollection<int>(1);
        collection.Add(0);
        var sink = new CollectionSink();
        collection.Observe(sink.Added, sink.Removed, sink.Replaced, sink.Moved, sink.Cleared);
        collection[0] = 1;

        Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 2; i < 100_002; i++) collection[0] = i;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Equal(100_001, sink.Last);
    }

    [Fact]
    public void ReactiveDictionary_SteadyStateReplace_AllocatesZeroBytes()
    {
        var dictionary = new ReactiveDictionary<int, int>(1);
        dictionary.Add(7, 0);
        var sink = new DictionarySink();
        dictionary.Observe(sink.Added, sink.Removed, sink.Replaced, sink.Cleared);
        dictionary[7] = 1;

        Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 2; i < 100_002; i++) dictionary[7] = i;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Equal(100_001, sink.Last);
    }

    [Fact]
    public async Task ManualSubscriptionTree_SteadyStateNotification_AllocatesZeroBytes()
    {
        using var scope = new LuminUiTestScope();
        LuminUIRuntime.RegisterAll();
        var model = new CounterModel();
        CounterScreenReaction.Source = model;
        var handle = await CounterScreen.OpenAsync();
        model.SetCount(4);

        Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100_000; i++) model.Increment();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Equal(100_004, handle.View.LastRendered);
        Assert.Equal(100_004, handle.View.Widget.LastRendered);
        Assert.True(await handle.CloseAsync());
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
