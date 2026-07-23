using Xunit;

namespace LuminUI.Tests;

public sealed class ReactiveBehaviorTests
{
    [Fact]
    public void Collection_Move_PreservesDataAndRaisesSingleMove()
    {
        var values = new ReactiveCollection<int>(3);
        values.Add(10);
        values.Add(20);
        values.Add(30);
        int moves = 0;
        int oldIndex = -1;
        int newIndex = -1;
        int moved = 0;
        values.Observe(
            static (_, _) => { }, static (_, _) => { }, static (_, _, _) => { },
            (oldValue, newValue, item) =>
            {
                moves++;
                oldIndex = oldValue;
                newIndex = newValue;
                moved = item;
            },
            static () => { });

        values.Move(2, 0);

        Assert.Equal(new[] { 30, 10, 20 }, values);
        Assert.Equal(1, moves);
        Assert.Equal(2, oldIndex);
        Assert.Equal(0, newIndex);
        Assert.Equal(30, moved);
    }

    [Fact]
    public void Dictionary_DistinguishesAddReplaceRemoveAndClear()
    {
        var values = new ReactiveDictionary<int, string>(4);
        int adds = 0, replaces = 0, removes = 0, clears = 0;
        values.Observe(
            (_, _) => adds++,
            (_, _) => removes++,
            (_, _, _) => replaces++,
            () => clears++);

        values.Add(1, "one");
        values[1] = "ONE";
        values[1] = "ONE";
        values.Add(2, "two");
        values.Remove(1);
        values.Clear();

        Assert.Equal(2, adds);
        Assert.Equal(1, replaces);
        Assert.Equal(1, removes);
        Assert.Equal(1, clears);
        Assert.Equal(5, values.Version);
    }
}
