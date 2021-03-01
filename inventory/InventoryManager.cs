using Godot;
using System;

public class InventoryManager : Node
{
    private InventoryDatabase _inventoryDatabase;

    private GameWorld _gameWorld;

    public override void _Ready()
    {
        _inventoryDatabase = (InventoryDatabase)GetNode("InventoryDatabase");
    }

    public void Initialize(GameWorld gameWorld)
    {
        _gameWorld = gameWorld;
    }

    public Godot.Collections.Array<ItemResource> GetPurchasableItems()
    {
        return _inventoryDatabase.GetItems();
    }

    public ItemResource GetPurchasableItemByID(String itemID)
    {
        return _inventoryDatabase.GetItemByID(itemID);
    }

    // Add item through unit cost in team
    public bool BuyItem(ItemResource itemResource, Inventory inventory)
    {
        if (inventory != null && itemResource != null && IsInstanceValid(inventory) && IsInstanceValid(itemResource))
        {

            if (inventory != null && IsInstanceValid(inventory) && inventory.GetCurrentTeamMapAI().ChargeAmount(itemResource.Price))
            {
                return AddItem(itemResource, inventory);
            }

            return false;
        }

        return false;
    }

    // Sell item to increase cost in team
    public bool SellItem(int inventoryIndex, Inventory inventory)
    {
        ItemResource itemResource = inventory.GetItem(inventoryIndex);

        if (inventory.RemoveItem(inventoryIndex))
        {
            // Debit back the amount
            inventory.GetCurrentTeamMapAI().ChargeAmount(-itemResource.Price);

            return true;
        }

        return false;
    }

    public bool AddItem(ItemResource itemResource, Inventory inventory)
    {
        if (inventory != null && itemResource != null && IsInstanceValid(inventory) && IsInstanceValid(itemResource))
        {
            inventory.AddItem(itemResource);
            return true;
        }

        return false;
    }

    public bool RemoveItem(int inventoryIndex, Inventory inventory)
    {
        if (inventory != null && IsInstanceValid(inventory) && inventory.GetItem(inventoryIndex) != null)
        {
            ItemResource itemResource = inventory.GetItem(inventoryIndex);

            inventory.RemoveItem(inventoryIndex);
            return true;
        }

        return false;
    }

    public void EquipItem(Inventory inventory, int inventoryItemIndex, Weapon.WeaponOrder weaponOrder, int weaponIndex)
    {
        if (inventory != null && IsInstanceValid(inventory))
        {
            inventory.EquipItem(inventoryItemIndex, weaponOrder, weaponIndex);
        }
    }

    public void UnequipItem(Inventory inventory, Weapon.WeaponOrder weaponOrder, int weaponIndex)
    {
        if (inventory != null && IsInstanceValid(inventory))
        {
            inventory.UnequipItem(weaponOrder, weaponIndex);

        }
    }

    public void DropItem(int inventoryIndex, Inventory inventory)
    {
        ItemResource itemResource = inventory.GetItem(inventoryIndex);

        if (inventory.RemoveItem(inventoryIndex))
        {
            Pickup pickup = (Pickup)((PackedScene)GD.Load("res://items/Pickup.tscn")).Instance();
            _gameWorld.AddChild(pickup);

            pickup.Initialize(this, itemResource);
            pickup.GlobalPosition = inventory.GetAgent().GlobalPosition + new Vector2(50f, 50f); 

        }
    }

    public void UseItem(int inventoryIndex, Inventory inventory)
    {
        GD.Print("TODO: IMPLEMENT USE LOGIC IN INVENTORY MANAGER");
    }
}
