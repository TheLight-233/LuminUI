using LuminUI.Attributes;
using LuminUI.Samples.Inventory.Model;
using LuminUI.Samples.Inventory.View;

namespace LuminUI.Samples.Inventory.Reaction;

[ReactionFor(typeof(ItemInventoryView))]
public sealed partial class ItemInventoryReaction
{
    protected override void OnBind()
        => View.BindItems(InventoryContext.Inventory.Items);
}

[ReactionFor(typeof(ItemCellView))]
public sealed partial class ItemCellReaction
{
    [OnClick(nameof(ItemCellView.SelectButton))]
    private void Select() => InventoryContext.Inventory.Select(View.ItemId);
}
