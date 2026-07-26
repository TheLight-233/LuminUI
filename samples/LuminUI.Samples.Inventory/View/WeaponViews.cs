using LuminUI.Attributes;
using LuminUI.Samples.Inventory.Model;

namespace LuminUI.Samples.Inventory.View;

[View]
public partial class WeaponInventoryView : LuminView
{
    [Widget("Toolbar")]
    private WeaponFilterView _filter = null!;

    [Element("Empty")]
    private Label _empty = null!;

    private static readonly Func<WeaponCellView> CellFactory = static () => new WeaponCellView();
    private static readonly Action<WeaponCellView, WeaponItem, int> CellBinder =
        static (cell, item, _) => cell.Render(item);
    private LuminWidgetList<WeaponCellView, WeaponItem>? _list;

    protected override void OnInit()
    {
        if (_list == null)
            _list = CreateWidgetList(
                "List/Content", "List/Content/Template", CellFactory, CellBinder, 12);
        else
            RegisterList(_list);
    }

    internal void BindWeapons(IReadOnlyReactiveCollection<WeaponItem> weapons)
    {
        _list!.Bind(weapons);
        _empty.Text = weapons.Count == 0 ? "No weapons match this filter" : string.Empty;
    }
}

[View]
public partial class WeaponFilterView : LuminView
{
    [Element("Filter")]
    private Label _filter = null!;

    [Element("NextFilter")]
    internal Button NextFilterButton = null!;

    [Element("SortPower")]
    internal Button SortPowerButton = null!;

    internal void RenderFilter(WeaponFilter filter)
        => _filter.Text = "Filter: " + filter;
}

[View]
public partial class WeaponCellView : LuminView
{
    [Element("Select")]
    internal Button SelectButton = null!;

    [Element("Name")]
    private Label _name = null!;

    [Element("Kind")]
    private Label _kind = null!;

    [Element("Attack")]
    private Label _attack = null!;

    [Element("Level")]
    private Label _level = null!;

    internal int ItemId { get; private set; }

    internal void Render(WeaponItem weapon)
    {
        ItemId = weapon.Id;
        _name.Text = weapon.Name;
        _kind.Text = weapon.Kind.ToString();
        _attack.SetInt(weapon.Attack);
        _level.SetInt(weapon.Level);
    }
}
