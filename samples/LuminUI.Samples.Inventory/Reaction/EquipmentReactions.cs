using LuminUI.Attributes;
using LuminUI.Samples.Inventory.Model;
using LuminUI.Samples.Inventory.View;

namespace LuminUI.Samples.Inventory.Reaction;

[ReactionFor(typeof(EquipmentInventoryView))]
public sealed partial class EquipmentInventoryReaction
{
    protected override void OnBind()
        => View.BindEquipment(InventoryContext.Inventory.Equipment);
}

[ReactionFor(typeof(EquipmentCellView))]
public sealed partial class EquipmentCellReaction
{
    [OnClick(nameof(EquipmentCellView.SelectButton))]
    private void Select() => InventoryContext.Inventory.Select(View.ItemId);
}

[ReactionFor(typeof(EquippedWeaponSlotView))]
public sealed partial class EquippedWeaponSlotReaction
{
    protected override void OnBind()
        => Subscribe(InventoryContext.Loadout.Weapon, View.Render);
}

[ReactionFor(typeof(EquippedHeadSlotView))]
public sealed partial class EquippedHeadSlotReaction
{
    protected override void OnBind()
        => Subscribe(InventoryContext.Loadout.Head, View.Render);
}

[ReactionFor(typeof(EquippedBodySlotView))]
public sealed partial class EquippedBodySlotReaction
{
    protected override void OnBind()
        => Subscribe(InventoryContext.Loadout.Body, View.Render);
}

[ReactionFor(typeof(EquippedAccessorySlotView))]
public sealed partial class EquippedAccessorySlotReaction
{
    protected override void OnBind()
        => Subscribe(InventoryContext.Loadout.Accessory, View.Render);
}
