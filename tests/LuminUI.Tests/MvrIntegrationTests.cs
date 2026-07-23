using LuminUI.Attributes;
using LuminUI.Testing;
using Xunit;

namespace LuminUI.Tests;

[LuminModel]
public sealed class CounterModel
{
    public ReactiveProperty<int> Count { get; } = new(3);

    [LuminAction]
    public void Increment() => Count.Value++;
}

[Screen(typeof(CounterModel), PoolSize = 1)]
public partial class CounterScreen : LuminView
{
    [UiWidget("Counter")]
    private CounterWidget _widget = null!;

    public int LastRendered { get; private set; }
    public CounterWidget Widget => _widget;
    public object ReactiveIdentity => Reactive;

    [Observe(nameof(CounterModel.Count))]
    private void Render(int count) => LastRendered = count;

    public void Increment() => Reactive.Increment();
    public void Finish(int result) => Close(result);
}

[View(typeof(CounterModel))]
public partial class CounterWidget : LuminView
{
    public int LastRendered { get; private set; }
    public object ReactiveIdentity => Reactive;

    [Observe(nameof(CounterModel.Count))]
    private void Render(int count) => LastRendered = count;
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
public sealed class ListModel
{
    public ReactiveCollection<int> Items { get; } = new(4) { 10, 20 };
}

[Screen(typeof(ListModel), PoolSize = 1)]
public partial class ListScreen : LuminView
{
    [BindList(nameof(ListModel.Items), "Items", "Items/Template", MaxIdle = 4)]
    private void ShowCell(CountingCell cell, int value, int index) => cell.Value = value;
}

[View]
public partial class CountingCell : LuminView
{
    public CountingCell() => Created++;
    public static int Created { get; set; }
    public int Value { get; set; }
}

public sealed class MvrIntegrationTests
{
    [Fact]
    public async Task GeneratedReactive_IsSharedByScreenAndWidget_AndUnsubscribesOnClose()
    {
        using var scope = new LuminUiTestScope();
        LuminUIRuntime.RegisterAll();
        var model = new CounterModel();

        var handle = await CounterScreen.OpenAsync(model);

        Assert.Equal(3, handle.View.LastRendered);
        Assert.Equal(3, handle.View.Widget.LastRendered);
        Assert.Same(handle.View.ReactiveIdentity, handle.View.Widget.ReactiveIdentity);

        handle.View.Increment();
        Assert.Equal(4, model.Count.Value);
        Assert.Equal(4, handle.View.LastRendered);
        Assert.Equal(4, handle.View.Widget.LastRendered);

        var screen = handle.View;
        var widget = screen.Widget;
        Assert.True(await handle.CloseAsync());

        model.Count.Value = 9;
        Assert.Equal(4, screen.LastRendered);
        Assert.Equal(4, widget.LastRendered);
    }

    [Fact]
    public async Task GeneratedPureScreen_OpensWithoutModel()
    {
        using var scope = new LuminUiTestScope();
        LuminUIRuntime.RegisterAll();

        var handle = await PureScreen.OpenAsync();

        Assert.True(handle.IsValid);
        Assert.True(await handle.CloseAsync());
    }

    [Fact]
    public async Task CancelledClose_RestoresOpenState_AndCanCloseAgain()
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
    public async Task ClosedHandle_KeepsOwnResult_WhenPooledViewIsReopened()
    {
        using var scope = new LuminUiTestScope();
        LuminUIRuntime.RegisterAll();
        var model = new CounterModel();
        var first = await CounterScreen.OpenAsync(model);

        first.View.Finish(42);
        var second = await CounterScreen.OpenAsync(model);

        Assert.Equal(42, await first.WaitForResultAsync<int>());
        Assert.False(first.IsValid);
        Assert.True(second.IsValid);
        Assert.True(await second.CloseAsync());
    }

    [Fact]
    public async Task PooledScreen_ReusesGeneratedWidgetAndListCells()
    {
        using var scope = new LuminUiTestScope();
        LuminUIRuntime.RegisterAll();
        var counter = new CounterModel();
        var firstCounter = await CounterScreen.OpenAsync(counter);
        var firstScreen = firstCounter.View;
        var firstWidget = firstCounter.View.Widget;
        Assert.True(await firstCounter.CloseAsync());

        var secondCounter = await CounterScreen.OpenAsync(counter);
        Assert.Same(firstScreen, secondCounter.View);
        Assert.Same(firstWidget, secondCounter.View.Widget);
        Assert.True(await secondCounter.CloseAsync());

        CountingCell.Created = 0;
        var listModel = new ListModel();
        var firstList = await ListScreen.OpenAsync(listModel);
        Assert.Equal(2, CountingCell.Created);
        Assert.True(await firstList.CloseAsync());

        var secondList = await ListScreen.OpenAsync(listModel);
        Assert.Equal(2, CountingCell.Created);
        Assert.True(await secondList.CloseAsync());
    }
}
