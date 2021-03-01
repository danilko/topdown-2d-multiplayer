using Godot;
using System;

public class Inventory : Node
{
    [Signal]
    public delegate void InventoryChangeSignal();

    [Signal]
    public delegate void WeaponChangeSignal();

    private int _maxItemCapacity = 20;

    private int _availableCapacity;

    private Godot.Collections.Array<ItemResource> _items = new Godot.Collections.Array<ItemResource>();

    private Godot.Collections.Dictionary<String, int> _equipmentIndex = new Godot.Collections.Dictionary<String, int>();
    private Godot.Collections.Array<int> _usedIndex = new Godot.Collections.Array<int>();

    private Agent _agent;

    public override void _Ready()
    {
        _availableCapacity = _maxItemCapacity;

        for (int index = 0; index < _maxItemCapacity; index++)
        {
            _items.Add(null);
        }
    }

    public void Initialize(Agent agent)
    {
        _agent = agent;
        EmitSignal(nameof(InventoryChangeSignal));
    }

    public TeamMapAI GetCurrentTeamMapAI()
    {
        return _agent.GetCurrentTeamMapAI();
    }

    public Godot.Collections.Array<ItemResource> GetItems()
    {
        return _items;
    }

    public Agent GetAgent()
    {
        return _agent;
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
                if (itemResource.ItemID == _items[index].ItemID)
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
            if (_items[index] != null && itemResource.ItemID == _items[index].ItemID && !isItemIndexInUsed(index))
            {
                foundIndex = index;
            }
        }

        if (foundIndex != -1)
        {
            _items[foundIndex] = null;
            _availableCapacity++;
            EmitSignal(nameof(InventoryChangeSignal));
            return true;
        }

        return false;
    }

    public bool RemoveItem(int index)
    {
        if (_items[index] == null)
        {
            return false;
        }

        if (!isItemIndexInUsed(index))
        {
            _items[index] = null;
            _availableCapacity++;
            EmitSignal(nameof(InventoryChangeSignal));
        }

        return true;
    }

    public bool AddItem(ItemResource itemResource)
    {
        int foundIndex = -1;

        for (int index = 0; index < _items.Count; index++)
        {
            if (_items[index] == null)
            {
                foundIndex = index;
            }
        }

        if (foundIndex != -1)
        {
            _items[foundIndex] = itemResource;
            _availableCapacity--;
            EmitSignal(nameof(InventoryChangeSignal));
            return true;
        }

        return false;
    }

    public int GetAvailableCapacity()
    {
        return _availableCapacity;
    }

    public bool isItemIndexInUsed(int index)
    {
        return _usedIndex.Contains(index);
    }

    public int GetEquipItemIndex(Weapon.WeaponOrder weaponOrder, int weaponIndex)
    {
        int index = -1;

        if (_equipmentIndex.ContainsKey(weaponOrder + "_" + weaponIndex))
        {
            index = _equipmentIndex[weaponOrder + "_" + weaponIndex];
        }

        return index;
    }

    // Equip weapon at given index
    public void EquipItem(int itemIndex, Weapon.WeaponOrder weaponOrder, int weaponIndex)
    {
        if (isItemIndexInUsed(itemIndex))
        {
            return;
        }

        // Unequip weapon first
        UnequipItem(weaponOrder, weaponIndex);

        _equipmentIndex.Add(weaponOrder + "_" + weaponIndex, itemIndex);
        _usedIndex.Add(itemIndex);
        _agent.EquipWeapon(_items[itemIndex].ReferencePackedScene, weaponOrder, weaponIndex);

        EmitSignal(nameof(WeaponChangeSignal), weaponOrder, weaponIndex);
    }

    // Unequip weapon at given index
    public void UnequipItem(Weapon.WeaponOrder weaponOrder, int weaponIndex)
    {
        int itemIndex = GetEquipItemIndex(weaponOrder, weaponIndex);

        _agent.UnequipWeapon(weaponOrder, weaponIndex);
        _equipmentIndex.Remove(weaponOrder + "_" + weaponIndex);
        _usedIndex.Remove(itemIndex);

        EmitSignal(nameof(WeaponChangeSignal), weaponOrder, weaponIndex);
    }
}
