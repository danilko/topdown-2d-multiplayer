using Godot;
using System;

public class InventoryManager : Node
{
    private InventoryDatabase _inventoryDatabase;
    public override void _Ready()
    {
        _inventoryDatabase = (InventoryDatabase)GetNode("InventoryDatabase");
    }

    public Godot.Collections.Array<ItemResource> GetPurchasableItems()
    {
        return _inventoryDatabase.GetItems();
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
    public bool SellItem(ItemResource itemResource, Inventory inventory)
    {
        if (inventory != null && itemResource != null && IsInstanceValid(inventory) && IsInstanceValid(itemResource) && inventory.HasItem(itemResource))
        {
            if(RemoveItem(itemResource, inventory))
            {
                inventory.GetCurrentTeamMapAI().AddAmount(itemResource.Price);
            }
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

    public bool RemoveItem(ItemResource itemResource, Inventory inventory)
    {
        if (inventory != null && itemResource != null && IsInstanceValid(inventory) && IsInstanceValid(itemResource) && inventory.HasItem(itemResource))
        {
            inventory.RemoveItem(itemResource);
            return true;
        }

        return false;
    }
}
