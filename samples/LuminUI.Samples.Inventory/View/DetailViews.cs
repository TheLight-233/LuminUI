using LuminUI.Attributes;
using LuminUI.Samples.Inventory.Model;

namespace LuminUI.Samples.Inventory.View;

[View]
public partial class InventoryDetailView : LuminView
{
    [Widget("Stats")]
    private InventoryStatsView _stats = null!;

    [Widget("Commands")]
    private InventoryCommandBarView _commands = null!;

    [Element("Name")]
    private Label _name = null!;

    [Element("Category")]
    private Label _category = null!;

    [Element("Description")]
    private Label _description = null!;

    [Element("Rarity")]
    private Label _rarity = null!;

    internal void RenderEntry(InventoryEntry? entry)
    {
        _name.Text = entry?.Name ?? "Nothing selected";
        _category.Text = entry switch
        {
            WeaponItem => "Weapon",
            EquipmentItem => "Equipment",
            ConsumableItem => "Item",
            _ => string.Empty
        };
        _description.Text = entry?.Description ?? "Select an inventory entry to inspect it.";
        _rarity.Text = entry == null ? string.Empty : "Rarity " + entry.Rarity;
    }
}

[View]
public partial class InventoryStatsView : LuminView
{
    [Element("Attack")]
    private Label _attack = null!;

    [Element("Defense")]
    private Label _defense = null!;

    [Element("Count")]
    private Label _count = null!;

    [Element("Extra")]
    private Label _extra = null!;

    internal void RenderStats(InventoryEntry? entry)
    {
        _attack.SetInt(entry is WeaponItem weapon ? weapon.Attack : 0);
        _defense.SetInt(entry is EquipmentItem equipment ? equipment.Defense : 0);
        _count.SetInt(entry?.Count ?? 0);
        _extra.Text = entry switch
        {
            WeaponItem selectedWeapon => selectedWeapon.Kind + " / Level " + selectedWeapon.Level,
            EquipmentItem selectedEquipment => selectedEquipment.SetName + " set",
            ConsumableItem item => item.Effect,
            _ => string.Empty
        };
    }
}

[View]
public partial class InventoryCommandBarView : LuminView
{
    [Element("Equip")]
    internal Button EquipButton = null!;

    [Element("Use")]
    internal Button UseButton = null!;

    [Element("Upgrade")]
    internal Button UpgradeButton = null!;

    [Element("BuyCapacity")]
    internal Button BuyCapacityButton = null!;

    [Element("Status")]
    private Label _status = null!;

    internal void RenderCommands(InventoryEntry? selected, int gold, int capacity)
    {
        EquipButton.Enabled = selected is WeaponItem or EquipmentItem;
        UseButton.Enabled = selected is ConsumableItem;
        int upgradePrice = selected is WeaponItem weapon ? 100 + weapon.Level * 25 : int.MaxValue;
        UpgradeButton.Enabled = selected is WeaponItem && gold >= upgradePrice;
        UpgradeButton.Text = selected is WeaponItem ? "Upgrade: " + upgradePrice : "Upgrade";
        BuyCapacityButton.Enabled = gold >= 250;
        BuyCapacityButton.Text = "Expand to " + (capacity + 5);
        _status.Text = selected == null ? "Select an entry" : "Ready";
    }
}

[View]
public partial class InventoryComparisonView : LuminView
{
    [Element("Selected")]
    private Label _selected = null!;

    [Element("Equipped")]
    private Label _equipped = null!;

    [Element("Delta")]
    private Label _delta = null!;

    [Element("LiveState")]
    private Label _liveState = null!;

    [Element("ToggleLive")]
    internal Button ToggleLiveButton = null!;

    internal void RenderComparison(InventoryEntry? selected, InventoryEntry? equipped)
    {
        _selected.Text = selected?.Name ?? "No comparison";
        _equipped.Text = equipped?.Name ?? "Nothing equipped";

        int selectedValue = selected switch
        {
            WeaponItem weapon => weapon.Attack,
            EquipmentItem equipment => equipment.Defense,
            _ => 0
        };
        int equippedValue = equipped switch
        {
            WeaponItem weapon => weapon.Attack,
            EquipmentItem equipment => equipment.Defense,
            _ => 0
        };
        _delta.SetInt(selectedValue - equippedValue);
    }

    internal void RenderLiveState(bool listening)
    {
        _liveState.Text = listening ? "Live comparison on" : "Live comparison paused";
        ToggleLiveButton.Text = listening ? "Pause live" : "Resume live";
    }
}
