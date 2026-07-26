using LuminUI.Attributes;
using LuminUI.Samples.Inventory.Model;

namespace LuminUI.Samples.Inventory.View;

[Screen(Layer = UILayer.Popup, Mode = UIMode.Stack, PoolSize = 1)]
public partial class InventoryScreenView : LuminView
{
    [Widget("Header")]
    private InventoryHeaderView _header = null!;

    [Widget("CategoryTabs")]
    private InventoryCategoryTabsView _categoryTabs = null!;

    [Widget("Content/Weapons")]
    private WeaponInventoryView _weapons = null!;

    [Widget("Content/Equipment")]
    private EquipmentInventoryView _equipment = null!;

    [Widget("Content/Items")]
    private ItemInventoryView _items = null!;

    [Widget("Detail")]
    private InventoryDetailView _detail = null!;

    [Element("ComparisonSlot")]
    private Panel _comparisonSlot = null!;

    private InventoryComparisonView? _comparison;

    protected override void OnInit()
    {
        // Runtime widgets are reused with the pooled Screen but remounted every open.
        _comparison ??= new InventoryComparisonView();
        AddWidget(_comparison, "ComparisonSlot");
    }

    internal void RenderCategory(InventoryCategory category)
    {
        SetWidgetVisible(_weapons, category == InventoryCategory.Weapons);
        SetWidgetVisible(_equipment, category == InventoryCategory.Equipment);
        SetWidgetVisible(_items, category == InventoryCategory.Items);
    }

    internal void RenderComparisonVisibility(InventoryEntry? selected)
    {
        if (_comparison == null) return;
        SetWidgetVisible(_comparison, selected is WeaponItem or EquipmentItem);
    }

    public bool HasNestedWidgets => _equipment.Children.Count > 0;
    public bool ComparisonMounted => ReferenceEquals(_comparison?.Parent, this);
}

[View]
public partial class InventoryHeaderView : LuminView
{
    [Element("Gold")]
    private Label _gold = null!;

    [Element("Capacity")]
    private Label _capacity = null!;

    [Element("Power")]
    private Label _power = null!;

    internal void RenderGold(int gold) => _gold.SetInt(gold);
    internal void RenderCapacity(int used, int capacity) => _capacity.SetPair(used, capacity);
    internal void RenderPower(int power) => _power.SetInt(power);
}

[View]
public partial class InventoryCategoryTabsView : LuminView
{
    [Element("Weapons")]
    internal Button WeaponsButton = null!;

    [Element("Equipment")]
    internal Button EquipmentButton = null!;

    [Element("Items")]
    internal Button ItemsButton = null!;

    internal void RenderActiveCategory(InventoryCategory category)
    {
        WeaponsButton.Selected = category == InventoryCategory.Weapons;
        EquipmentButton.Selected = category == InventoryCategory.Equipment;
        ItemsButton.Selected = category == InventoryCategory.Items;
    }
}
