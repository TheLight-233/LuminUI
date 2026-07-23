using System.Diagnostics;
using LuminUI;

const int Operations = 1_000_000;

var property = new ReactiveProperty<int>();
var propertySink = new ValueSink();
property.SubscribeNoPush(propertySink.Set);
property.Value = -1;

var collection = new ReactiveCollection<int>(1) { 0 };
var collectionSink = new CollectionSink();
collection.Observe(collectionSink.Add, collectionSink.Remove,
    collectionSink.Replace, collectionSink.Move, collectionSink.Clear);
collection[0] = -1;

var dictionary = new ReactiveDictionary<int, int>(1) { [7] = 0 };
var dictionarySink = new DictionarySink();
dictionary.Observe(dictionarySink.Add, dictionarySink.Remove,
    dictionarySink.Replace, dictionarySink.Clear);
dictionary[7] = -1;

Run("ReactiveProperty set+notify", () =>
{
    for (int i = 0; i < Operations; i++) property.Value = i;
});

Run("ReactiveCollection replace", () =>
{
    for (int i = 0; i < Operations; i++) collection[0] = i;
});

Run("ReactiveDictionary replace", () =>
{
    for (int i = 0; i < Operations; i++) dictionary[7] = i;
});

static void Run(string name, Action action)
{
    action();
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    long before = GC.GetAllocatedBytesForCurrentThread();
    long started = Stopwatch.GetTimestamp();
    action();
    long stopped = Stopwatch.GetTimestamp();
    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    double milliseconds = (stopped - started) * 1000.0 / Stopwatch.Frequency;

    Console.WriteLine($"{name,-30} {milliseconds,8:F2} ms  {allocated,6} B");
    if (allocated != 0)
        throw new InvalidOperationException(name + " allocated " + allocated + " bytes.");
}

sealed class ValueSink
{
    public int Value;
    public void Set(int value) => Value = value;
}

sealed class CollectionSink
{
    public int Value;
    public void Add(int _, int value) => Value = value;
    public void Remove(int _, int value) => Value = value;
    public void Replace(int _, int __, int value) => Value = value;
    public void Move(int _, int __, int value) => Value = value;
    public void Clear() => Value = 0;
}

sealed class DictionarySink
{
    public int Value;
    public void Add(int _, int value) => Value = value;
    public void Remove(int _, int value) => Value = value;
    public void Replace(int _, int __, int value) => Value = value;
    public void Clear() => Value = 0;
}
