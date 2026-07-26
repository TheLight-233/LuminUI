namespace LuminUI.Samples.Inventory.Model;

public enum InventoryCategory
{
    Weapons,
    Equipment,
    Items
}

public enum WeaponKind
{
    Sword,
    Bow,
    Catalyst
}

public enum WeaponFilter
{
    All,
    Sword,
    Bow,
    Catalyst
}

public enum EquipmentSlot
{
    Head,
    Body,
    Accessory
}

public abstract class InventoryEntry
{
    protected InventoryEntry(
        int id, string name, string description, int rarity, int count)
    {
        Id = id;
        Name = name;
        Description = description;
        Rarity = rarity;
        Count = count;
    }

    public int Id { get; }
    public string Name { get; }
    public string Description { get; }
    public int Rarity { get; }
    public int Count { get; }
}

public sealed class WeaponItem : InventoryEntry
{
    public WeaponItem(
        int id, string name, string description, int rarity,
        WeaponKind kind, int attack, int level)
        : base(id, name, description, rarity, 1)
    {
        Kind = kind;
        Attack = attack;
        Level = level;
    }

    public WeaponKind Kind { get; }
    public int Attack { get; }
    public int Level { get; }

    public WeaponItem Upgrade()
        => new(Id, Name, Description, Rarity, Kind, Attack + 12, Level + 1);
}

public sealed class EquipmentItem : InventoryEntry
{
    public EquipmentItem(
        int id, string name, string description, int rarity,
        EquipmentSlot slot, int defense, string setName)
        : base(id, name, description, rarity, 1)
    {
        Slot = slot;
        Defense = defense;
        SetName = setName;
    }

    public EquipmentSlot Slot { get; }
    public int Defense { get; }
    public string SetName { get; }
}

public sealed class ConsumableItem : InventoryEntry
{
    public ConsumableItem(
        int id, string name, string description, int rarity,
        int count, string effect)
        : base(id, name, description, rarity, count)
        => Effect = effect;

    public string Effect { get; }

    public ConsumableItem WithCount(int count)
        => new(Id, Name, Description, Rarity, count, Effect);
}
