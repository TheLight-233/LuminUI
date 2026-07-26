using LuminUI.Attributes;
using LuminUI.Samples.Inventory.Model;
using LuminUI.Samples.Inventory.View;

namespace LuminUI.Samples.Inventory.Reaction;

[ReactionFor(typeof(InventoryDetailView))]
public sealed partial class InventoryDetailReaction
{
    protected override void OnBind()
        => Subscribe(InventoryContext.Inventory.Selected, View.RenderEntry);
}

[ReactionFor(typeof(InventoryStatsView))]
public sealed partial class InventoryStatsReaction
{
    protected override void OnBind()
        => Subscribe(InventoryContext.Inventory.Selected, View.RenderStats);
}

[ReactionFor(typeof(InventoryCommandBarView))]
public sealed partial class InventoryCommandBarReaction
{
    private InventoryEntry? _selected;
    private int _gold;
    private int _capacity;

    protected override void OnBind()
    {
        Subscribe(InventoryContext.Inventory.Selected, OnSelected);
        Subscribe(InventoryContext.Wallet.Gold, OnGold);
        Subscribe(InventoryContext.Inventory.Capacity, OnCapacity);
    }

    [OnClick(nameof(InventoryCommandBarView.EquipButton))]
    private void Equip() => InventoryContext.Commands.EquipSelected();

    [OnClick(nameof(InventoryCommandBarView.UseButton))]
    private void Use() => InventoryContext.Commands.UseSelected();

    [OnClick(nameof(InventoryCommandBarView.UpgradeButton))]
    private void Upgrade() => InventoryContext.Commands.UpgradeSelectedWeapon();

    [OnClick(nameof(InventoryCommandBarView.BuyCapacityButton))]
    private void BuyCapacity() => InventoryContext.Commands.BuyCapacity();

    private void OnSelected(InventoryEntry? selected)
    {
        _selected = selected;
        Render();
    }

    private void OnGold(int gold)
    {
        _gold = gold;
        Render();
    }

    private void OnCapacity(int capacity)
    {
        _capacity = capacity;
        Render();
    }

    private void Render() => View.RenderCommands(_selected, _gold, _capacity);
}

[ReactionFor(typeof(InventoryComparisonView))]
public sealed partial class InventoryComparisonReaction
{
    private InventoryEntry? _selected;
    private WeaponItem? _weapon;
    private EquipmentItem? _head;
    private EquipmentItem? _body;
    private EquipmentItem? _accessory;
    private SubscriptionHandle _selectedSubscription;

    protected override void OnBind()
    {
        _selectedSubscription = Subscribe(
            InventoryContext.Inventory.Selected, OnSelected);
        Subscribe(InventoryContext.Loadout.Weapon, OnWeapon);
        Subscribe(InventoryContext.Loadout.Head, OnHead);
        Subscribe(InventoryContext.Loadout.Body, OnBody);
        Subscribe(InventoryContext.Loadout.Accessory, OnAccessory);
        View.RenderLiveState(true);
    }

    [OnClick(nameof(InventoryComparisonView.ToggleLiveButton))]
    private void ToggleLive()
    {
        if (_selectedSubscription.IsActive)
        {
            Unsubscribe(ref _selectedSubscription);
            View.RenderLiveState(false);
            return;
        }

        _selectedSubscription = Subscribe(
            InventoryContext.Inventory.Selected, OnSelected);
        View.RenderLiveState(true);
    }

    private void OnSelected(InventoryEntry? selected)
    {
        _selected = selected;
        Render();
    }

    private void OnWeapon(WeaponItem? weapon)
    {
        _weapon = weapon;
        Render();
    }

    private void OnHead(EquipmentItem? head)
    {
        _head = head;
        Render();
    }

    private void OnBody(EquipmentItem? body)
    {
        _body = body;
        Render();
    }

    private void OnAccessory(EquipmentItem? accessory)
    {
        _accessory = accessory;
        Render();
    }

    private void Render()
    {
        InventoryEntry? equipped = _selected switch
        {
            WeaponItem => _weapon,
            EquipmentItem { Slot: EquipmentSlot.Head } => _head,
            EquipmentItem { Slot: EquipmentSlot.Body } => _body,
            EquipmentItem => _accessory,
            _ => null
        };
        View.RenderComparison(_selected, equipped);
    }
}
