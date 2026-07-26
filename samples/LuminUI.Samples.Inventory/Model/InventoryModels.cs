using LuminUI.Attributes;

namespace LuminUI.Samples.Inventory.Model;

[LuminModel]
public sealed partial class InventoryModel
{
    private readonly List<WeaponItem> _allWeapons = new(8);
    private readonly ReactiveCollection<WeaponItem> _visibleWeapons = new(8);
    private readonly ReactiveCollection<EquipmentItem> _equipment = new(8);
    private readonly ReactiveCollection<ConsumableItem> _items = new(16);
    private readonly ReactiveProperty<InventoryCategory> _activeCategory =
        new(InventoryCategory.Weapons);
    private readonly ReactiveProperty<InventoryEntry?> _selected = new(null);
    private readonly ReactiveProperty<WeaponFilter> _activeWeaponFilter =
        new(WeaponFilter.All);
    private readonly ReactiveProperty<int> _capacity = new(40);
    private readonly ReactiveProperty<int> _usedSlots = new(0);

    public InventoryModel()
    {
        _allWeapons.Add(new WeaponItem(
            1001, "Iron Sword", "A balanced sword for close combat.",
            2, WeaponKind.Sword, 42, 1));
        _allWeapons.Add(new WeaponItem(
            1002, "Falcon Bow", "A light bow with a fast draw speed.",
            3, WeaponKind.Bow, 57, 4));
        _allWeapons.Add(new WeaponItem(
            1003, "Ember Codex", "A catalyst carrying a warm elemental core.",
            4, WeaponKind.Catalyst, 76, 8));
        _allWeapons.Add(new WeaponItem(
            1004, "Knight Blade", "A heavy sword issued to royal guards.",
            4, WeaponKind.Sword, 81, 9));

        _equipment.Add(new EquipmentItem(
            2001, "Scout Hood", "Light head protection for field travel.",
            2, EquipmentSlot.Head, 18, "Scout"));
        _equipment.Add(new EquipmentItem(
            2002, "Guardian Coat", "A reinforced coat for frontline defense.",
            4, EquipmentSlot.Body, 47, "Guardian"));
        _equipment.Add(new EquipmentItem(
            2003, "Amber Ring", "A ring that stabilizes elemental energy.",
            3, EquipmentSlot.Accessory, 23, "Amber"));

        _items.Add(new ConsumableItem(
            3001, "Health Potion", "Restores health immediately.",
            2, 8, "Restore 500 HP"));
        _items.Add(new ConsumableItem(
            3002, "Attack Meal", "Temporarily increases attack power.",
            3, 3, "Attack +20% for 300s"));
        _items.Add(new ConsumableItem(
            3003, "Revival Feather", "Revives one defeated party member.",
            4, 1, "Revive with 30% HP"));

        RebuildVisibleWeapons();
        RefreshUsedSlots();
        _selected.Value = _allWeapons[0];
    }

    internal InventoryEntry? SelectedEntry => _selected.Value;

    public void SetCategory(InventoryCategory category)
        => _activeCategory.Value = category;

    public void Select(int itemId)
    {
        var entry = FindEntry(itemId);
        if (entry != null) _selected.Value = entry;
    }

    public void CycleWeaponFilter()
    {
        _activeWeaponFilter.Value = _activeWeaponFilter.Value switch
        {
            WeaponFilter.All => WeaponFilter.Sword,
            WeaponFilter.Sword => WeaponFilter.Bow,
            WeaponFilter.Bow => WeaponFilter.Catalyst,
            _ => WeaponFilter.All
        };
        RebuildVisibleWeapons();
    }

    public void SortWeaponsByPower()
    {
        _allWeapons.Sort(static (left, right) => right.Attack.CompareTo(left.Attack));
        RebuildVisibleWeapons();
    }

    internal WeaponItem? UpgradeSelectedWeapon()
    {
        if (_selected.Value is not WeaponItem selected) return null;
        int index = _allWeapons.FindIndex(item => item.Id == selected.Id);
        if (index < 0) return null;

        var upgraded = selected.Upgrade();
        _allWeapons[index] = upgraded;
        _selected.Value = upgraded;
        RebuildVisibleWeapons();
        return upgraded;
    }

    internal bool ConsumeSelectedItem()
    {
        if (_selected.Value is not ConsumableItem selected) return false;
        int index = IndexOf(_items, selected.Id);
        if (index < 0) return false;

        if (selected.Count > 1)
        {
            var remaining = selected.WithCount(selected.Count - 1);
            _items[index] = remaining;
            _selected.Value = remaining;
        }
        else
        {
            _items.RemoveAt(index);
            _selected.Value = _items.Count > 0 ? _items[0] : null;
        }

        RefreshUsedSlots();
        return true;
    }

    internal void IncreaseCapacity(int amount)
        => _capacity.Value += amount;

    private InventoryEntry? FindEntry(int itemId)
    {
        for (int i = 0; i < _allWeapons.Count; i++)
            if (_allWeapons[i].Id == itemId) return _allWeapons[i];
        for (int i = 0; i < _equipment.Count; i++)
            if (_equipment[i].Id == itemId) return _equipment[i];
        for (int i = 0; i < _items.Count; i++)
            if (_items[i].Id == itemId) return _items[i];
        return null;
    }

    private void RebuildVisibleWeapons()
    {
        _visibleWeapons.Clear();
        for (int i = 0; i < _allWeapons.Count; i++)
        {
            var weapon = _allWeapons[i];
            if (MatchesFilter(weapon.Kind, _activeWeaponFilter.Value))
                _visibleWeapons.Add(weapon);
        }
    }

    private static bool MatchesFilter(WeaponKind kind, WeaponFilter filter)
        => filter == WeaponFilter.All || (int)kind == (int)filter - 1;

    private void RefreshUsedSlots()
        => _usedSlots.Value = _allWeapons.Count + _equipment.Count + _items.Count;

    private static int IndexOf<T>(IReadOnlyList<T> entries, int id)
        where T : InventoryEntry
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].Id == id) return i;
        return -1;
    }
}

[LuminModel]
public sealed partial class PlayerLoadoutModel
{
    private readonly ReactiveProperty<WeaponItem?> _weapon = new(null);
    private readonly ReactiveProperty<EquipmentItem?> _head = new(null);
    private readonly ReactiveProperty<EquipmentItem?> _body = new(null);
    private readonly ReactiveProperty<EquipmentItem?> _accessory = new(null);
    private readonly ReactiveProperty<int> _power = new(0);

    public bool Equip(InventoryEntry entry)
    {
        switch (entry)
        {
            case WeaponItem weapon:
                _weapon.Value = weapon;
                break;
            case EquipmentItem { Slot: EquipmentSlot.Head } head:
                _head.Value = head;
                break;
            case EquipmentItem { Slot: EquipmentSlot.Body } body:
                _body.Value = body;
                break;
            case EquipmentItem accessory:
                _accessory.Value = accessory;
                break;
            default:
                return false;
        }

        RefreshPower();
        return true;
    }

    private void RefreshPower()
    {
        int attack = _weapon.Value?.Attack ?? 0;
        int defense = (_head.Value?.Defense ?? 0)
            + (_body.Value?.Defense ?? 0)
            + (_accessory.Value?.Defense ?? 0);
        _power.Value = attack + defense;
    }
}

[LuminModel]
public sealed partial class WalletModel
{
    private readonly ReactiveProperty<int> _gold = new(1800);

    public bool TrySpend(int amount)
    {
        if (amount < 0 || _gold.Value < amount) return false;
        _gold.Value -= amount;
        return true;
    }

    public void AddGold(int amount)
    {
        if (amount > 0) _gold.Value += amount;
    }
}
