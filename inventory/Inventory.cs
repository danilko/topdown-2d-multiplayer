using Godot;
using System;

public class Inventory : Node
{
    [Signal]
    public delegate void InventoryChangeSignal();

    [Signal]
    public delegate void WeaponChangeSignal();

    private int _maxItemCapacity = 12;

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
        if (index < 0 || index >= _items.Count)
        {
            return null;
        }

        return _items[index];
    }

    public int GetItemIndex(String itemID)
    {
        int foundIndex = -1;
        if (_items.Count > 0)
        {
            for (int index = 0; index < _items.Count; index++)
            {
                if (_items[index] != null && itemID == _items[index].ItemID)
                {
                    foundIndex = index;
                }
            }
        }

        return foundIndex;
    }

    public Boolean HasItem(ItemResource itemResource)
    {
        return GetItemIndex(itemResource.ItemID) != -1;
    }

    public void RemoveItem(int index)
    {
        _items[index] = null;
        _availableCapacity++;
        EmitSignal(nameof(InventoryChangeSignal));
    }

    public void AddItem(ItemResource itemResource, int foundIndex = -1)
    {
        if (foundIndex == -1)
        {
            for (int index = 0; index < _items.Count; index++)
            {
                if (_items[index] == null)
                {
                    foundIndex = index;
                }
            }
        }

        if (foundIndex != -1)
        {
            _items[foundIndex] = itemResource;
            _availableCapacity--;
            EmitSignal(nameof(InventoryChangeSignal));
        }
    }

    public int GetAvailableCapacity()
    {
        return _availableCapacity;
    }

    public bool IsItemIndexInUsed(int index)
    {
        return _usedIndex.Contains(index);
    }

    public int GetEquipItemIndex(Weapon.WeaponOrder weaponOrder, int weaponIndex)
    {
        int index = -1;

        if (_equipmentIndex.ContainsKey((int)weaponOrder + "_" + weaponIndex))
        {
            index = _equipmentIndex[(int)weaponOrder + "_" + weaponIndex];
        }

        return index;
    }

    // Equip weapon at given index
    public void EquipItem(int itemIndex, Weapon.WeaponOrder weaponOrder, int weaponIndex)
    {
        if (IsItemIndexInUsed(itemIndex))
        {
            return;
        }

        // Unequip weapon first
        UnequipItem(weaponOrder, weaponIndex);
        String wepaonKey = (int)weaponOrder + "_" + weaponIndex;
        _equipmentIndex.Add(wepaonKey, itemIndex);
        _usedIndex.Add(itemIndex);
        _agent.EquipWeapon(_items[itemIndex].ReferencePackedScene, weaponOrder, weaponIndex);

        if (weaponOrder == Weapon.WeaponOrder.Left && weaponIndex == 1)
        {
            GD.Print("EQUIP " + _agent.GetUnitName() + " " + weaponOrder + weaponIndex + (_agent.GetWeapons(weaponOrder)[weaponIndex] != null));

        }

        EmitSignal(nameof(WeaponChangeSignal), weaponOrder, weaponIndex);
        EmitSignal(nameof(InventoryChangeSignal));
    }

    // Unequip weapon at given index
    public void UnequipItem(Weapon.WeaponOrder weaponOrder, int weaponIndex)
    {
        int itemIndex = GetEquipItemIndex(weaponOrder, weaponIndex);

        _agent.UnequipWeapon(weaponOrder, weaponIndex);
        _equipmentIndex.Remove((int)weaponOrder + "_" + weaponIndex);
        _usedIndex.Remove(itemIndex);

        EmitSignal(nameof(WeaponChangeSignal), weaponOrder, weaponIndex);
        EmitSignal(nameof(InventoryChangeSignal));
    }


    public String GetInventoryState()
    {
        // Set the current inventory count
        String state = "" + (_items.Count - _availableCapacity) + ";";

        for (int index = 0; index < _items.Count; index++)
        {
            if (_items[index] != null)
            {
                state = state + index + ";" + _items[index].ItemID + ";";
            }
        }

        // sync used item
        state = state + _usedIndex.Count + ";";

        for (int index = 0; index < _usedIndex.Count; index++)
        {
            state = state + _usedIndex[index] + ";";
        }

        // Need to go through all weapons
        String weaponInfo = "";
        int equipmentIndex = 0;

        for (int weaponOrderIndex = 0; weaponOrderIndex <= (int)Weapon.WeaponOrder.Left; weaponOrderIndex++)
        {
            Weapon.WeaponOrder currentWeaponOrder = (Weapon.WeaponOrder)weaponOrderIndex;
            int weaponCount = _agent.GetWeapons(currentWeaponOrder).Count;
            for (int weaponIndex = 0; weaponIndex < weaponCount; weaponIndex++)
            {
                String weaponKey = weaponOrderIndex + "_" + weaponIndex;
                int weaponItemIndex = -1;
                int ammo = -1;

                if (_equipmentIndex.ContainsKey(weaponKey))
                {
                    weaponItemIndex = _equipmentIndex[weaponKey];
                    ammo = _agent.GetWeapons(currentWeaponOrder)[weaponIndex].GetAmmo();
                }

                weaponInfo = weaponInfo + weaponKey + ";" + weaponItemIndex + ";" + ammo + ";";

                equipmentIndex++;
            }
        }

        state = state + equipmentIndex + ";" + weaponInfo;

        return state;
    }


    public void SyncInventoryState(String state, InventoryDatabase inventoryDatabase)
    {
        String[] stateInfo = state.Split(";");
        GD.Print("SYNC INVENTORY PAYLOAD " + _agent.GetUnitName() + " " + state);
        int stateIndex = 0;

        // Set the current inventory count
        int inventoryCount = int.Parse(stateInfo[stateIndex]);
        _availableCapacity = _items.Count - inventoryCount;

        stateIndex++;

        // Clean up the inventory
        for (int index = 0; index < _items.Count; index++)
        {
            _items[index] = null;
        }

        for (int index = 0; index < inventoryCount; index++)
        {
            int inventoryInex = int.Parse(stateInfo[stateIndex]);
            stateIndex++;
            _items[inventoryInex] = inventoryDatabase.GetItemByID(stateInfo[stateIndex]);
            stateIndex++;
        }

        // sync used item
        int usedIndex = int.Parse(stateInfo[stateIndex]);
        stateIndex++;

        // Clean up the used index
        _usedIndex.Clear();

        for (int index = 0; index < usedIndex; index++)
        {
            _usedIndex.Add(int.Parse(stateInfo[stateIndex]));
            stateIndex++;
        }

        int equipCount = int.Parse(stateInfo[stateIndex]);
        stateIndex++;

        _equipmentIndex.Clear();

        for (int index = 0; index < equipCount; index++)
        {
            String weaponKey = stateInfo[stateIndex];
            stateIndex++;

            int itemIndex = int.Parse(stateInfo[stateIndex]);
            stateIndex++;

            int ammo = int.Parse(stateInfo[stateIndex]);
            stateIndex++;

            Weapon.WeaponOrder weaponOrder = (Weapon.WeaponOrder)int.Parse(weaponKey.Split("_")[0]);
            int weaponIndex = int.Parse(weaponKey.Split("_")[1]);

            if (itemIndex != -1)
            {
                _equipmentIndex.Add(weaponKey, itemIndex);

                if (_agent.GetWeapons(weaponOrder)[weaponIndex] == null || _agent.GetWeapons(weaponOrder)[weaponIndex].ItemResourceID != _items[itemIndex].ItemID)
                {
                    // Client is not equip same as server, try to enforced it
                    _agent.UnequipWeapon(weaponOrder, weaponIndex);
                    _agent.EquipWeapon(_items[itemIndex].ReferencePackedScene, weaponOrder, weaponIndex);
                    _agent.GetWeapons(weaponOrder)[weaponIndex].SetAmmo(ammo);
                    EmitSignal(nameof(WeaponChangeSignal), weaponOrder, weaponIndex);
                }
            }
            else
            {
                if (_agent.GetWeapons(weaponOrder)[weaponIndex] != null)
                {
                    // Client is equip while server does not, try to enforced it
                    _agent.UnequipWeapon(weaponOrder, weaponIndex);
                    EmitSignal(nameof(WeaponChangeSignal), weaponOrder, weaponIndex);
                }
            }
        }

        EmitSignal(nameof(InventoryChangeSignal));
    }
}
