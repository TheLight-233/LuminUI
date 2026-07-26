using LuminUI.Attributes;
using LuminUI.Samples.Inventory.Model;

namespace LuminUI.Samples.Inventory.View;

[View]
public partial class ItemInventoryView : LuminView
{
    private static readonly Func<ItemCellView> CellFactory = static () => new ItemCellView();
    private static readonly Action<ItemCellView, ConsumableItem, int> CellBinder =
        static (cell, item, _) => cell.Render(item);
    private LuminWidgetList<ItemCellView, ConsumableItem>? _list;

    protected override void OnInit()
    {
        if (_list == null)
            _list = CreateWidgetList(
                "List/Content", "List/Content/Template", CellFactory, CellBinder, 12);
        else
            RegisterList(_list);
    }

    internal void BindItems(IReadOnlyReactiveCollection<ConsumableItem> items)
        => _list!.Bind(items);
}

[View]
public partial class ItemCellView : LuminView
{
    [Element("Select")]
    internal Button SelectButton = null!;

    [Element("Name")]
    private Label _name = null!;

    [Element("Count")]
    private Label _count = null!;

    [Element("Effect")]
    private Label _effect = null!;

    internal int ItemId { get; private set; }

    internal void Render(ConsumableItem item)
    {
        ItemId = item.Id;
        _name.Text = item.Name;
        _count.SetInt(item.Count);
        _effect.Text = item.Effect;
    }
}
