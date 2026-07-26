namespace LuminUI.Samples.Inventory.Model;

public sealed class InventoryService
{
    private readonly InventoryModel _inventory;
    private readonly PlayerLoadoutModel _loadout;
    private readonly WalletModel _wallet;

    public InventoryService(
        InventoryModel inventory, PlayerLoadoutModel loadout, WalletModel wallet)
    {
        _inventory = inventory;
        _loadout = loadout;
        _wallet = wallet;
    }

    public bool EquipSelected()
    {
        var selected = _inventory.SelectedEntry;
        return selected != null && _loadout.Equip(selected);
    }

    public bool UseSelected() => _inventory.ConsumeSelectedItem();

    public bool UpgradeSelectedWeapon()
    {
        if (_inventory.SelectedEntry is not WeaponItem weapon) return false;
        int price = 100 + weapon.Level * 25;
        if (!_wallet.TrySpend(price)) return false;

        var upgraded = _inventory.UpgradeSelectedWeapon();
        if (upgraded == null) return false;
        _loadout.Equip(upgraded);
        return true;
    }

    public bool BuyCapacity()
    {
        const int price = 250;
        if (!_wallet.TrySpend(price)) return false;
        _inventory.IncreaseCapacity(5);
        return true;
    }
}

public static class InventoryContext
{
    public static InventoryModel Inventory { get; } = new();
    public static PlayerLoadoutModel Loadout { get; } = new();
    public static WalletModel Wallet { get; } = new();
    public static InventoryService Commands { get; } =
        new(Inventory, Loadout, Wallet);
}
