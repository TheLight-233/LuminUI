using LuminUI.Attributes;
using LuminUI.Samples.Inventory.Model;
using LuminUI.Samples.Inventory.View;

namespace LuminUI.Samples.Inventory.Reaction;

[ReactionFor(typeof(InventoryScreenView))]
public sealed partial class InventoryScreenReaction
{
    private static InventoryModel Model => InventoryContext.Inventory;

    protected override void OnBind()
    {
        Subscribe(Model.ActiveCategory, View.RenderCategory);
        Subscribe(Model.Selected, View.RenderComparisonVisibility);
    }
}

[ReactionFor(typeof(InventoryHeaderView))]
public sealed partial class InventoryHeaderReaction
{
    private int _used;
    private int _capacity;

    protected override void OnBind()
    {
        Subscribe(InventoryContext.Wallet.Gold, View.RenderGold);
        Subscribe(InventoryContext.Inventory.UsedSlots, OnUsedSlots);
        Subscribe(InventoryContext.Inventory.Capacity, OnCapacity);
        Subscribe(InventoryContext.Loadout.Power, View.RenderPower);
    }

    private void OnUsedSlots(int used)
    {
        _used = used;
        View.RenderCapacity(_used, _capacity);
    }

    private void OnCapacity(int capacity)
    {
        _capacity = capacity;
        View.RenderCapacity(_used, _capacity);
    }
}

[ReactionFor(typeof(InventoryCategoryTabsView))]
public sealed partial class InventoryCategoryTabsReaction
{
    private static InventoryModel Model => InventoryContext.Inventory;

    protected override void OnBind()
        => Subscribe(Model.ActiveCategory, View.RenderActiveCategory);

    [OnClick(nameof(InventoryCategoryTabsView.WeaponsButton))]
    private void ShowWeapons() => Model.SetCategory(InventoryCategory.Weapons);

    [OnClick(nameof(InventoryCategoryTabsView.EquipmentButton))]
    private void ShowEquipment() => Model.SetCategory(InventoryCategory.Equipment);

    [OnClick(nameof(InventoryCategoryTabsView.ItemsButton))]
    private void ShowItems() => Model.SetCategory(InventoryCategory.Items);
}
