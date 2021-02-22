using Godot;
using System;

public class Inventory : Node
{
    [Signal]
    public delegate void InventoryChangeSignal();

    [Export]
    private Godot.Collections.Array<ItemResource> _items;

    [Export]
    private Godot.Collections.Array<ItemResource> _rightWeapons;
    
    [Export]
    private Godot.Collections.Array<ItemResource> _leftWeapons;

    private Agent _agent;

    public override void _Ready()
    {
        _items = new Godot.Collections.Array<ItemResource>();

        _rightWeapons = new Godot.Collections.Array<ItemResource>();
        _rightWeapons.Add(null);
        _rightWeapons.Add(null);
        _rightWeapons.Add(null);
        
        _leftWeapons = new Godot.Collections.Array<ItemResource>();
        _leftWeapons.Add(null);
        _leftWeapons.Add(null);
        _leftWeapons.Add(null);

        EmitSignal(nameof(InventoryChangeSignal), this);
    }

    public void Initialize(Agent agent)
    {
        _agent = agent;
    }

    public TeamMapAI GetCurrentTeamMapAI()
    {
        return _agent.GetCurrentTeamMapAI();
    }

    public Godot.Collections.Array<ItemResource> GetItems()
    {
        return _items;
    }

    public Godot.Collections.Array<ItemResource> GetLeftWeapons()
    {
        return _leftWeapons;
    }

    public Godot.Collections.Array<ItemResource> GetRightWeapons()
    {
        return _rightWeapons;
    }

    public ItemResource GetItem(int index)
    {
        return _items[index];
    }

    public Boolean HasItem(ItemResource itemResource)
    {
        if (_items.Count > 0)
        {
            int foundIndex = -1;

            for (int index = 0; index < _items.Count; index++)
            {
                if (itemResource.Name == _items[index].Name)
                {
                    foundIndex = index;
                }
            }

            if (foundIndex != -1)
            {
                return true;
            }
        }

        return false;
    }

    public bool RemoveItem(ItemResource itemResource)
    {
        int foundIndex = -1;

        for (int index = 0; index < _items.Count; index++)
        {
            if (itemResource.Name == _items[index].Name)
            {
                foundIndex = index;
            }
        }

        if (foundIndex != -1)
        {
            _items.RemoveAt(foundIndex);
            return true;
        }

        return false;
    }

    public void AddItem(ItemResource itemResource)
    {
        _items.Add(itemResource);
    }
}
