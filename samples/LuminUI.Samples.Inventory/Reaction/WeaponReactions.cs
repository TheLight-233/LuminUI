using LuminUI.Attributes;
using LuminUI.Samples.Inventory.Model;
using LuminUI.Samples.Inventory.View;

namespace LuminUI.Samples.Inventory.Reaction;

[ReactionFor(typeof(WeaponInventoryView))]
public sealed partial class WeaponInventoryReaction
{
    protected override void OnBind()
        => View.BindWeapons(InventoryContext.Inventory.VisibleWeapons);
}

[ReactionFor(typeof(WeaponFilterView))]
public sealed partial class WeaponFilterReaction
{
    private static InventoryModel Model => InventoryContext.Inventory;

    protected override void OnBind()
        => Subscribe(Model.ActiveWeaponFilter, View.RenderFilter);

    [OnClick(nameof(WeaponFilterView.NextFilterButton))]
    private void NextFilter() => Model.CycleWeaponFilter();

    [OnClick(nameof(WeaponFilterView.SortPowerButton))]
    private void SortPower() => Model.SortWeaponsByPower();
}

[ReactionFor(typeof(WeaponCellView))]
public sealed partial class WeaponCellReaction
{
    [OnClick(nameof(WeaponCellView.SelectButton))]
    private void Select() => InventoryContext.Inventory.Select(View.ItemId);
}
