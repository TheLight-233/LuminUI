using LuminUI.Attributes;
using LuminUI.Samples.Inventory.Model;

namespace LuminUI.Samples.Inventory.View;

[View]
public partial class EquipmentInventoryView : LuminView
{
    [Widget("Loadout")]
    private EquipmentLoadoutView _loadout = null!;

    private static readonly Func<EquipmentCellView> CellFactory =
        static () => new EquipmentCellView();
    private static readonly Action<EquipmentCellView, EquipmentItem, int> CellBinder =
        static (cell, item, _) => cell.Render(item);
    private LuminWidgetList<EquipmentCellView, EquipmentItem>? _list;

    protected override void OnInit()
    {
        if (_list == null)
            _list = CreateWidgetList(
                "List/Content", "List/Content/Template", CellFactory, CellBinder, 12);
        else
            RegisterList(_list);
    }

    internal void BindEquipment(IReadOnlyReactiveCollection<EquipmentItem> equipment)
        => _list!.Bind(equipment);
}

// A pure structural Widget: it has no Reaction, but its child slots do.
[View]
public partial class EquipmentLoadoutView : LuminView
{
    [Widget("Weapon")]
    private EquippedWeaponSlotView _weapon = null!;

    [Widget("Head")]
    private EquippedHeadSlotView _head = null!;

    [Widget("Body")]
    private EquippedBodySlotView _body = null!;

    [Widget("Accessory")]
    private EquippedAccessorySlotView _accessory = null!;
}

[View]
public partial class EquipmentCellView : LuminView
{
    [Element("Select")]
    internal Button SelectButton = null!;

    [Element("Name")]
    private Label _name = null!;

    [Element("Slot")]
    private Label _slot = null!;

    [Element("Defense")]
    private Label _defense = null!;

    internal int ItemId { get; private set; }

    internal void Render(EquipmentItem equipment)
    {
        ItemId = equipment.Id;
        _name.Text = equipment.Name;
        _slot.Text = equipment.Slot.ToString();
        _defense.SetInt(equipment.Defense);
    }
}

[View]
public partial class EquippedWeaponSlotView : LuminView
{
    [Element("Name")]
    private Label _name = null!;

    [Element("Power")]
    private Label _power = null!;

    internal void Render(WeaponItem? weapon)
    {
        _name.Text = weapon?.Name ?? "No weapon equipped";
        _power.SetInt(weapon?.Attack ?? 0);
    }
}

[View]
public partial class EquippedHeadSlotView : LuminView
{
    [Element("Name")]
    private Label _name = null!;

    [Element("Defense")]
    private Label _defense = null!;

    internal void Render(EquipmentItem? equipment)
    {
        _name.Text = equipment?.Name ?? "Head slot empty";
        _defense.SetInt(equipment?.Defense ?? 0);
    }
}

[View]
public partial class EquippedBodySlotView : LuminView
{
    [Element("Name")]
    private Label _name = null!;

    [Element("Defense")]
    private Label _defense = null!;

    internal void Render(EquipmentItem? equipment)
    {
        _name.Text = equipment?.Name ?? "Body slot empty";
        _defense.SetInt(equipment?.Defense ?? 0);
    }
}

[View]
public partial class EquippedAccessorySlotView : LuminView
{
    [Element("Name")]
    private Label _name = null!;

    [Element("Defense")]
    private Label _defense = null!;

    internal void Render(EquipmentItem? equipment)
    {
        _name.Text = equipment?.Name ?? "Accessory slot empty";
        _defense.SetInt(equipment?.Defense ?? 0);
    }
}
