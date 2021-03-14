using Godot;
using System;

public class InventoryUI : PopupPanel
{

    private InventoryManager _inventoryManager;
    private Inventory _inventory;

    private Panel _itemPanel;
    private GridContainer _gridContainerStore;
    private GridContainer _gridContainerInventory;
    private GridContainer _gridContainerWeaponSlots;
    private GridContainer _gridContainerWeaponChoices;
    public override void _Ready()
    {

        _itemPanel = (Panel)GetNode("ItemPanel");
        _gridContainerStore = (GridContainer)GetNode("TabContainer/Store/Scrollable/GridContainerStore");
        _gridContainerInventory = (GridContainer)GetNode("TabContainer/Inventory/Scrollable/GridContainerInventory");
        _gridContainerWeaponSlots = (GridContainer)GetNode("TabContainer/Assembly/VSplitContainer/WeaponSlotScrollable/GridContainerWeaponSlots");
        _gridContainerWeaponChoices = (GridContainer)GetNode("TabContainer/Assembly/VSplitContainer/WeaponChoiceScrollable/GridContainerWeaponChoice");
    }

    public void Initialize(InventoryManager inventoryManager, Inventory inventory)
    {
        _inventoryManager = inventoryManager;
        _inventory = inventory;

        // Populate list
        _populateGridContainer(_gridContainerStore);
        _populateGridContainer(_gridContainerInventory);

        _populateWeaponSlots();

        _inventory.Connect(nameof(Inventory.WeaponChangeSignal), this, nameof(_updateWeaponChange));
        _inventory.Connect(nameof(Inventory.InventoryChangeSignal), this, nameof(_updateInventoryChange));

        _inventory.EmitSignal(nameof(Inventory.InventoryChangeSignal));
    }

    private void _updateInventoryChange()
    {
        _cleanGridContainer(_gridContainerWeaponChoices);
        _populateGridContainer(_gridContainerInventory);
    }

    private void _updateWeaponChange(Weapon.WeaponOrder weaponOrder, int weaponIndex)
    {
        String name = "WeaponSlotPanel_" + weaponOrder + "_" + weaponIndex;
        WeaponSlotPanel currentWeaponSlotPanel = (WeaponSlotPanel)_gridContainerWeaponSlots.GetNode(name);

        Agent agent = _inventory.GetAgent();
        Weapon weapon = agent.GetWeapons(weaponOrder)[weaponIndex];

        ItemResource itemResource = null;

        if (weapon != null)
        {
            itemResource = _inventoryManager.GetPurchasableItemByID(weapon.ItemResourceID);
        }

        currentWeaponSlotPanel.Initialize(itemResource, weaponOrder, weaponIndex);

        _populateWeaponChoices(weaponOrder, weaponIndex);
    }

    private void _populateWeaponSlots()
    {
        for (int weaponOrderIndex = 0; weaponOrderIndex <= (int)Weapon.WeaponOrder.Left; weaponOrderIndex++)
        {
            Weapon.WeaponOrder weaponOrder = (Weapon.WeaponOrder)weaponOrderIndex;

            Agent agent = _inventory.GetAgent();
            if (agent != null && IsInstanceValid(agent))
            {
                Godot.Collections.Array<Weapon> weapons = agent.GetWeapons(weaponOrder);
                for (int weaponIndex = 0; weaponIndex < weapons.Count; weaponIndex++)
                {
                    String name = "WeaponSlotPanel_" + weaponOrder + "_" + weaponIndex;

                    WeaponSlotPanel currentWeaponSlotPanel = null;

                    // If panel does not exist, created it
                    if (!_gridContainerWeaponSlots.HasNode(name))
                    {
                        currentWeaponSlotPanel = (WeaponSlotPanel)GetNode("WeaponSlotPanel").Duplicate();
                        currentWeaponSlotPanel.Name = name;
                        _gridContainerWeaponSlots.AddChild(currentWeaponSlotPanel);

                        currentWeaponSlotPanel.Connect(nameof(WeaponSlotPanel.WeaponSlotPanelClickSignal), this, nameof(_populateWeaponChoices));

                    }
                    else
                    {
                        currentWeaponSlotPanel = (WeaponSlotPanel)_gridContainerWeaponSlots.GetNode(name);
                    }

                    ItemResource itemResource = null;

                    if (weapons[weaponIndex] != null)
                    {
                        itemResource = _inventoryManager.GetPurchasableItemByID(weapons[weaponIndex].ItemResourceID);
                    }

                    // Initialize with current weapon
                    currentWeaponSlotPanel.Initialize(itemResource, weaponOrder, weaponIndex);
                    currentWeaponSlotPanel.Show();
                }
            }
        }
    }

    private void _populateWeaponChoices(Weapon.WeaponOrder weaponOrder, int weaponIndex)
    {
        _cleanGridContainer(_gridContainerWeaponChoices);

        int inventoryItemIndex = 0;
        foreach (ItemResource itemResource in _inventory.GetItems())
        {
            if (itemResource != null && itemResource.CurrentItemType == ItemResource.ItemType.EQUIPABLE)
            {
                ItemPanel panel = (ItemPanel)_itemPanel.Duplicate();

                _gridContainerWeaponChoices.AddChild(panel);

                populateWeaponChoicePanel(panel, itemResource, inventoryItemIndex, weaponOrder, weaponIndex);
            }

            inventoryItemIndex++;

        }
    }

    private void populateWeaponChoicePanel(ItemPanel itemPanel, ItemResource itemResource, int inventoryItemIndex, Weapon.WeaponOrder weaponOrder, int weaponIndex)
    {
        itemPanel.Initialize(itemResource, ItemPanel.ItemPanelType.WEAPON, inventoryItemIndex, weaponOrder, weaponIndex);

        itemPanel.SetUsed(_inventory.IsItemIndexInUsed(inventoryItemIndex));
        itemPanel.SetEquipped(_inventory.GetEquipItemIndex(weaponOrder, weaponIndex) == inventoryItemIndex);

        itemPanel.Show();

        itemPanel.Connect(nameof(ItemPanel.ItemPanelWeaponEquipClickSignal), this, nameof(_equipWeapon));
        itemPanel.Connect(nameof(ItemPanel.ItemPanelWeaponUnequipClickSignal), this, nameof(_unequipWeapon));
    }


    private void populatedInventoryPanel(ItemPanel itemPanel, ItemResource itemResource, int inventoryItemIndex)
    {
        itemPanel.Initialize(itemResource, ItemPanel.ItemPanelType.INVENTORY, inventoryItemIndex, Weapon.WeaponOrder.Right, -1);
        itemPanel.SetUsed(_inventory.IsItemIndexInUsed(inventoryItemIndex));
        itemPanel.Connect(nameof(ItemPanel.ItemPanelSaleClickSignal), this, nameof(_sellItem));

        itemPanel.Show();

        itemPanel.Connect(nameof(ItemPanel.ItemPanelInventoryUseClickSignal), this, nameof(_useItem));
        itemPanel.Connect(nameof(ItemPanel.ItemPanelInventoryDropClickSignal), this, nameof(_dropItem));
    }

    private void _equipWeapon(int inventoryItemIndex, Weapon.WeaponOrder weaponOrder, int weaponIndex)
    {
        _inventoryManager.EquipItem(_inventory, inventoryItemIndex, weaponOrder, weaponIndex);
    }
    private void _unequipWeapon(Weapon.WeaponOrder weaponOrder, int weaponIndex)
    {
        _inventoryManager.UnequipItem(_inventory, weaponOrder, weaponIndex);
    }

    private void _cleanGridContainer(GridContainer gridContainer)
    {
        if (gridContainer.GetChildCount() != 0)
        {
            foreach (Node node in gridContainer.GetChildren())
            {
                node.QueueFree();
            }
        }
    }

    private void _populateGridContainer(GridContainer gridContainer)
    {
        _cleanGridContainer(gridContainer);

        Godot.Collections.Array<ItemResource> items = null;
        if (gridContainer.Name == _gridContainerStore.Name)
        {
            items = _inventoryManager.GetPurchasableItems();
        }
        else
        {
            items = _inventory.GetItems();
        }

        int index = 0;
        foreach (ItemResource itemResource in items)
        {
            if (itemResource != null)
            {
                ItemPanel panel = (ItemPanel)_itemPanel.Duplicate();

                gridContainer.AddChild(panel);
                if (gridContainer.Name == _gridContainerStore.Name)
                {
                    populatedStorePanel(panel, itemResource, index);
                }
                else
                {
                    populatedInventoryPanel(panel, itemResource, index);
                }
            }

            index++;

        }
    }

    private void _purchaseItem(ItemResource itemResource)
    {
        _inventoryManager.PurchaseItem(itemResource, _inventory);
    }

    private void _sellItem(int inventoryIndex)
    {
        _inventoryManager.SellItem(inventoryIndex, _inventory);
    }

    private void _useItem(int inventoryIndex)
    {
        _inventoryManager.UseItem(inventoryIndex, _inventory);
    }

    private void _dropItem(int inventoryIndex)
    {
        _inventoryManager.RemoveItem(inventoryIndex, _inventory, true);
    }

    private void populatedStorePanel(ItemPanel itemPanel, ItemResource itemResource, int itemIndex)
    {
        itemPanel.Initialize(itemResource, ItemPanel.ItemPanelType.PURCHASE, itemIndex, Weapon.WeaponOrder.Right, -1);
        itemPanel.Show();

        itemPanel.Connect(nameof(ItemPanel.ItemPanelPurchaseClickSignal), this, nameof(_purchaseItem));
    }
}
