using Godot;
using System;

public class InventoryUI : PopupPanel
{

    private InventoryManager _inventoryManager;
    private Inventory _inventory;

    private Panel _itemPanel;
    private GridContainer _gridContainerStore;
    private GridContainer _gridContainerInventory;

    public override void _Ready()
    {

        _itemPanel = (Panel)GetNode("ItemPanel");
        _gridContainerStore = (GridContainer)GetNode("TabContainer/Store/Scrollable/GridContainerStore");
        _gridContainerInventory = (GridContainer)GetNode("TabContainer/Inventory/Scrollable/GridContainerInventory");
    }

    public void Initialize(InventoryManager inventoryManager, Inventory inventory)
    {
        _inventoryManager = inventoryManager;
        _inventory = inventory;


        // Clean up existing list
        _cleanGridContainer(_gridContainerStore);
        _cleanGridContainer(_gridContainerInventory);

        _populateGridContainer(_gridContainerStore);
        _populateGridContainer(_gridContainerInventory);
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
        Godot.Collections.Array<ItemResource> items = null;
        if (gridContainer.Name == _gridContainerStore.Name)
        {
            items = _inventoryManager.GetPurchasableItems();
        }
        else
        {
            items = _inventory.GetItems();
        }

        foreach (ItemResource itemResource in items)
        {
            ItemPanel panel = (ItemPanel)_itemPanel.Duplicate();

            gridContainer.AddChild(panel);
            if (gridContainer.Name == _gridContainerStore.Name)
            {
                populatedStorePanel(itemResource, panel);
            }
            else
            {
                populatedInventoryPanel(itemResource, panel);
            }

        }
    }

    private void purchaseItem(ItemResource itemResource)
    {
        _inventoryManager.BuyItem(itemResource,_inventory);

        // Clean up existing list
        _cleanGridContainer(_gridContainerInventory);
        _populateGridContainer(_gridContainerInventory);
    }

    private void populatedStorePanel(ItemResource itemResource, ItemPanel panel)
    {
        panel.Initialize(itemResource);
        panel.Show();

        panel.Connect(nameof(ItemPanel.ItemPanelClickSignal), this, nameof(purchaseItem));
    }
    private void populatedInventoryPanel(ItemResource itemResource, ItemPanel panel)
    {
        panel.Initialize(itemResource);
        panel.Show();

    }

}
