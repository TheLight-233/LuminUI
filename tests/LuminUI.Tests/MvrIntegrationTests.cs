using LuminUI.Attributes;
using LuminUI.Testing;
using Xunit;

namespace LuminUI.Tests;

[LuminModel]
public sealed partial class CounterModel
{
    private readonly ReactiveProperty<int> _count = new(3);

    public void Increment() => _count.Value++;
    public void SetCount(int value) => _count.Value = value;
}

[Screen(PoolSize = 1)]
public partial class CounterScreen : LuminView
{
    [Widget("Counter")]
    private CounterWidget _widget = null!;

    public int LastRendered { get; private set; }
    public CounterWidget Widget => _widget;

    internal void Render(int count) => LastRendered = count;
    public void Finish(int result) => Close(result);
}

[ReactionFor(typeof(CounterScreen))]
public sealed partial class CounterScreenReaction
{
    private SubscriptionHandle _countSubscription;

    public static CounterModel Source { get; set; } = new();
    public bool IsListening => _countSubscription.IsActive;

    protected override void OnBind()
        => _countSubscription = Subscribe(Source.Count, View.Render);

    public void Resume()
    {
        if (!_countSubscription.IsActive)
            _countSubscription = Subscribe(Source.Count, View.Render);
    }

    public void Pause() => Unsubscribe(ref _countSubscription);
}

[View]
public partial class CounterWidget : LuminView
{
    public int LastRendered { get; private set; }

    internal void Render(int count) => LastRendered = count;
}

[ReactionFor(typeof(CounterWidget))]
public sealed partial class CounterWidgetReaction
{
    private SubscriptionHandle _countSubscription;

    public bool IsListening => _countSubscription.IsActive;

    protected override void OnBind()
        => _countSubscription = Subscribe(CounterScreenReaction.Source.Count, View.Render);
}

[Screen(PoolSize = 0)]
public partial class PureScreen : LuminView { }

[Screen(PoolSize = 0)]
public partial class CancelableScreen : LuminView
{
    protected override LuminThread.LuminTask<bool> OnCloseAnimation(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return LuminThread.LuminTask.FromResult(true);
    }

    public void CloseFromInside() => Close();
}

[LuminModel]
public sealed partial class ListModel
{
    private readonly ReactiveCollection<int> _items = new(4);

    public ListModel()
    {
        _items.Add(10);
        _items.Add(20);
    }

    public void Add(int value) => _items.Add(value);
}

[LuminModel]
public sealed partial class SubscriptionModel
{
    private readonly ReactiveCollection<int> _items = new(4);
    private readonly ReactiveDictionary<int, int> _values = new(4);

    public void Add(int value)
    {
        _items.Add(value);
        _values.Add(value, value);
    }
}

[Screen(PoolSize = 0)]
public partial class CollectionDictionarySubscriptionScreen : LuminView
{
    public int CollectionAdds { get; private set; }
    public int DictionaryAdds { get; private set; }

    internal void OnCollectionAdded(int index, int value) => CollectionAdds++;
    internal void OnDictionaryAdded(int key, int value) => DictionaryAdds++;
}

[ReactionFor(typeof(CollectionDictionarySubscriptionScreen))]
public sealed partial class CollectionDictionarySubscriptionReaction
{
    public static SubscriptionModel Source { get; set; } = new();

    protected override void OnBind()
    {
        Subscribe(Source.Items, View.OnCollectionAdded, IgnoreCollectionItem,
            IgnoreCollectionReplace, IgnoreCollectionMove, IgnoreCollectionClear);
        Subscribe(Source.Values, View.OnDictionaryAdded, IgnoreDictionaryItem,
            IgnoreDictionaryReplace, IgnoreDictionaryClear);
    }

    private static void IgnoreCollectionItem(int index, int value) { }
    private static void IgnoreCollectionReplace(int index, int oldValue, int newValue) { }
    private static void IgnoreCollectionMove(int oldIndex, int newIndex, int value) { }
    private static void IgnoreCollectionClear() { }
    private static void IgnoreDictionaryItem(int key, int value) { }
    private static void IgnoreDictionaryReplace(int key, int oldValue, int newValue) { }
    private static void IgnoreDictionaryClear() { }
}

[Screen(PoolSize = 1)]
public partial class ListScreen : LuminView
{
    private static readonly Func<CountingCell> CellFactory = CreateCell;
    private static readonly Action<CountingCell, int, int> CellBinder = ShowCell;
    private LuminWidgetList<CountingCell, int>? _list;

    protected override void OnInit()
    {
        if (_list == null)
            _list = CreateWidgetList("Items", "Items/Template", CellFactory, CellBinder, 4);
        else
            RegisterList(_list);
    }

    internal void BindItems(IReadOnlyReactiveCollection<int> items) => _list!.Bind(items);

    private static CountingCell CreateCell() => new();
    private static void ShowCell(CountingCell cell, int value, int index) => cell.Value = value;
}

[ReactionFor(typeof(ListScreen))]
public sealed partial class ListScreenReaction
{
    public static ListModel Source { get; set; } = new();

    protected override void OnBind() => View.BindItems(Source.Items);
}

[View]
public partial class CountingCell : LuminView
{
    public CountingCell() => Created++;
    public static int Created { get; set; }
    public int Value { get; set; }
}

[Screen(PoolSize = 0)]
public partial class WidgetTreeScreen : LuminView
{
    [Widget("First")]
    private TreeWidget _first = null!;

    [Widget("Second")]
    private TreeWidget _second = null!;

    public TreeWidget First => _first;
    public TreeWidget Second => _second;

    public void HideFirst() => HideWidget(_first);
    public void ShowFirst() => ShowWidget(_first);
}

[View]
public partial class TreeWidget : LuminView
{
    [Widget("Nested")]
    private TreeLeaf _leaf = null!;

    public TreeLeaf Leaf => _leaf;
}

[View]
public partial class TreeLeaf : LuminView { }

[Screen(PoolSize = 1)]
public partial class RuntimeWidgetTreeScreen : LuminView
{
    private TreeLeaf? _runtimeLeaf;

    public TreeLeaf RuntimeLeaf => _runtimeLeaf!;

    protected override void OnInit()
    {
        _runtimeLeaf ??= new TreeLeaf();
        AddWidget(_runtimeLeaf, "Runtime");
    }
}

public sealed class MvrIntegrationTests
{
    [Fact]
    public async Task GeneratedReactionsPushCurrentValueAndDetachOnClose()
    {
        using var scope = new LuminUiTestScope();
        LuminUIRuntime.RegisterAll();
        var model = new CounterModel();
        CounterScreenReaction.Source = model;

        var handle = await CounterScreen.OpenAsync();

        Assert.Equal(3, handle.View.LastRendered);
        Assert.Equal(3, handle.View.Widget.LastRendered);
        Assert.True(handle.View.__Reaction.IsListening);
        Assert.True(handle.View.Widget.__Reaction.IsListening);

        model.Increment();
        Assert.Equal(4, model.Count.Value);
        Assert.Equal(4, handle.View.LastRendered);
        Assert.Equal(4, handle.View.Widget.LastRendered);

        var screen = handle.View;
        var widget = screen.Widget;
        Assert.True(await handle.CloseAsync());

        model.SetCount(9);
        Assert.False(screen.__Reaction.IsListening);
        Assert.False(widget.__Reaction.IsListening);
        Assert.Equal(4, screen.LastRendered);
        Assert.Equal(4, widget.LastRendered);
    }

    [Fact]
    public async Task HideKeepsSubscriptionsAndManualUnsubscribeCanBeReversed()
    {
        using var scope = new LuminUiTestScope();
        LuminUIRuntime.RegisterAll();
        var model = new CounterModel();
        CounterScreenReaction.Source = model;
        var handle = await CounterScreen.OpenAsync();

        handle.Hide();
        model.SetCount(6);
        Assert.Equal(6, handle.View.LastRendered);
        Assert.True(handle.View.__Reaction.IsListening);

        handle.View.__Reaction.Pause();
        model.SetCount(7);
        Assert.Equal(6, handle.View.LastRendered);

        handle.View.__Reaction.Resume();
        Assert.Equal(7, handle.View.LastRendered);
        Assert.True(handle.View.__Reaction.IsListening);
        Assert.True(await handle.CloseAsync());
    }

    [Fact]
    public async Task GeneratedScreenOpensWithoutModel()
    {
        using var scope = new LuminUiTestScope();
        LuminUIRuntime.RegisterAll();

        var handle = await PureScreen.OpenAsync();

        Assert.True(handle.IsValid);
        Assert.True(await handle.CloseAsync());
    }

    [Fact]
    public async Task CollectionAndDictionarySubscriptionsFollowViewLifetime()
    {
        using var scope = new LuminUiTestScope();
        LuminUIRuntime.RegisterAll();
        var model = new SubscriptionModel();
        CollectionDictionarySubscriptionReaction.Source = model;
        var handle = await CollectionDictionarySubscriptionScreen.OpenAsync();

        model.Add(1);
        Assert.Equal(1, handle.View.CollectionAdds);
        Assert.Equal(1, handle.View.DictionaryAdds);

        var view = handle.View;
        Assert.True(await handle.CloseAsync());
        model.Add(2);
        Assert.Equal(1, view.CollectionAdds);
        Assert.Equal(1, view.DictionaryAdds);
    }

    [Fact]
    public async Task GeneratedWidgetTreeExposesParentChildrenAndNodeVisibility()
    {
        using var scope = new LuminUiTestScope();
        LuminUIRuntime.RegisterAll();
        var handle = await WidgetTreeScreen.OpenAsync();

        Assert.Equal(2, handle.View.Children.Count);
        Assert.Same(handle.View, handle.View.First.Parent);
        Assert.Same(handle.View, handle.View.Second.Parent);
        Assert.Single(handle.View.First.Children);
        Assert.Same(handle.View.First, handle.View.First.Leaf.Parent);

        handle.View.HideFirst();
        Assert.False(handle.View.First.IsNodeVisible);
        handle.View.ShowFirst();
        Assert.True(handle.View.First.IsNodeVisible);

        Assert.True(await handle.CloseAsync());
    }

    [Fact]
    public async Task RuntimeWidgetIsRemountedWhenPooledScreenReopens()
    {
        using var scope = new LuminUiTestScope();
        LuminUIRuntime.RegisterAll();
        var first = await RuntimeWidgetTreeScreen.OpenAsync();
        var screen = first.View;
        var leaf = screen.RuntimeLeaf;

        Assert.Same(screen, leaf.Parent);
        Assert.Single(screen.Children);
        Assert.True(await first.CloseAsync());
        Assert.Null(leaf.Parent);

        var second = await RuntimeWidgetTreeScreen.OpenAsync();
        Assert.Same(screen, second.View);
        Assert.Same(leaf, second.View.RuntimeLeaf);
        Assert.Same(second.View, leaf.Parent);
        Assert.Single(second.View.Children);
        Assert.True(await second.CloseAsync());
    }

    [Fact]
    public async Task CancelledCloseRestoresOpenStateAndCanCloseAgain()
    {
        using var scope = new LuminUiTestScope();
        LuminUIRuntime.RegisterAll();
        var handle = await CancelableScreen.OpenAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await handle.CloseAsync(cts.Token));

        Assert.True(handle.IsValid);
        Assert.Equal(LuminViewState.Open, handle.State);
        handle.View.CloseFromInside();
    }

    [Fact]
    public async Task ClosedHandleKeepsOwnResultWhenPooledViewIsReopened()
    {
        using var scope = new LuminUiTestScope();
        LuminUIRuntime.RegisterAll();
        CounterScreenReaction.Source = new CounterModel();
        var first = await CounterScreen.OpenAsync();

        first.View.Finish(42);
        var second = await CounterScreen.OpenAsync();

        Assert.Equal(42, await first.WaitForResultAsync<int>());
        Assert.False(first.IsValid);
        Assert.True(second.IsValid);
        Assert.True(await second.CloseAsync());
    }

    [Fact]
    public async Task PooledScreenReusesGeneratedWidgetAndManualListCells()
    {
        using var scope = new LuminUiTestScope();
        LuminUIRuntime.RegisterAll();
        CounterScreenReaction.Source = new CounterModel();
        var firstCounter = await CounterScreen.OpenAsync();
        var firstScreen = firstCounter.View;
        var firstWidget = firstCounter.View.Widget;
        var firstScreenReaction = firstCounter.View.__Reaction;
        var firstWidgetReaction = firstCounter.View.Widget.__Reaction;
        Assert.True(await firstCounter.CloseAsync());

        var secondCounter = await CounterScreen.OpenAsync();
        Assert.Same(firstScreen, secondCounter.View);
        Assert.Same(firstWidget, secondCounter.View.Widget);
        Assert.Same(firstScreenReaction, secondCounter.View.__Reaction);
        Assert.Same(firstWidgetReaction, secondCounter.View.Widget.__Reaction);
        Assert.True(await secondCounter.CloseAsync());

        CountingCell.Created = 0;
        ListScreenReaction.Source = new ListModel();
        var firstList = await ListScreen.OpenAsync();
        Assert.Equal(2, CountingCell.Created);
        Assert.True(await firstList.CloseAsync());

        var secondList = await ListScreen.OpenAsync();
        Assert.Equal(2, CountingCell.Created);
        Assert.True(await secondList.CloseAsync());
    }
}
