using Godot;
using System;

public class Inventory : Node
{    
    [Signal]
    public delegate void InventoryChangeSignal();

    [Export]
    private Godot.Collections.Array<ItemResource> _items;

    private InventoryDatabase _inventoryDatabase;

    public override void _Ready()
    {
        _items = new Godot.Collections.Array<ItemResource>();
        EmitSignal(nameof(InventoryChangeSignal), this);
    }

    public void Initialize(InventoryDatabase inventoryDatabase)
    {
        _inventoryDatabase = inventoryDatabase;
    }

    public Godot.Collections.Array<ItemResource> GetItems()
    {
        return _items;
    }

    public ItemResource GetItem(int index)
    {
        return _items[index];
    }

    public void AddItem(String itemName, int quantity)
    {
        if(quantity < 0)
        {
            GD.Print("Cannot add less than 0 item");
            return;
        }

        ItemResource itemResource = _inventoryDatabase.GetItem(itemName);

        if(itemResource == null)
        {
            GD.Print("Cannot load item");
            return;
        }        

        _items.Add(itemResource);
    }
}
