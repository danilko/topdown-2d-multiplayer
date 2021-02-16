using Godot;
using System;

public class InventoryDatabase : Node
{
    Godot.Collections.Array<ItemResource> _items;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _items = new Godot.Collections.Array<ItemResource>();
        Godot.Directory directory = new Godot.Directory();
        directory.Open("res://items");
        directory.ListDirBegin();

        String fileName = directory.GetNext();

        while (fileName != null && fileName.Length != 0)
        {
            if (!directory.CurrentIsDir() && fileName.Contains(".tres") && !fileName.Contains("ItemResource.tres") && !fileName.Contains("Pickup"))
            {
                ItemResource itemResource = ((ItemResource)GD.Load("res://items/" + fileName));
                
                _items.Add(itemResource);
            }
            fileName = directory.GetNext();
        }
        directory.ListDirEnd();
    }

    public Godot.Collections.Array<ItemResource> GetItems()
    {
        return _items;
    }

    public ItemResource GetItem(String itemName)
    {
        foreach (ItemResource item in _items)
        {
            if (item.Name == itemName)
            {
                return item;
            }
        }

        return null;
    }

}
