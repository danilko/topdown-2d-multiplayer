using Godot;
using System;

public class InventoryUI : PopupPanel
{

    private InventoryDatabase _inventoryDatabase;
    private Inventory _inventory;

    private Panel _itemPanel;
    private GridContainer _gridContainerStore;
    private GridContainer _gridContainerInventory;

    public override void _Ready()
    {

        _itemPanel = (Panel)GetNode("ItemPanel");
        _gridContainerStore = (GridContainer)GetNode("TabContainer/StoreMargin/StoreScrollable/Store");
        //_gridContainerInventory = (GridContainer)GetNode("TabContainer/Inventory");

        // Temporary code
        Initialize((InventoryDatabase)GetNode("InventoryDatabase"), (Inventory)GetNode("Inventory"));
        this.PopupCentered(this.RectSize);
    }

    public void Initialize(InventoryDatabase inventoryDatabase, Inventory inventory)
    {
        _inventoryDatabase = inventoryDatabase;
        _inventory = inventory;

        // // Clean up existing list
        // if (_gridContainerStore.GetChildCount() != 0)
        // {
        //     foreach (Node node in _gridContainerStore.GetChildren())
        //     {
        //         node.QueueFree();
        //     }
        // }

        foreach (ItemResource itemResource in _inventoryDatabase.GetItems())
        {
            Panel panel = (Panel)_itemPanel.Duplicate();
            _gridContainerStore.AddChild(panel);
            populatePanel(itemResource, panel);
        }
    }

    private void populatePanel(ItemResource itemResource, Panel panel)
    {
        ((Label)(panel.GetNode("Name"))).Text = itemResource.Name;
        ((Label)(panel.GetNode("Price"))).Text = "COST: " + itemResource.Price;
        ((Label)(panel.GetNode("Description"))).Text = itemResource.Description;
        ((TextureRect)(panel.GetNode("Image"))).Texture = itemResource.ReferenceTexture;
        ((TextureRect)(panel.GetNode("Image"))).RectScale = new Vector2(0.5f, 0.5f);
        panel.Show();
    }


}
