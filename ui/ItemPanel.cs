using Godot;
using System;

public class ItemPanel : Panel
{
    [Signal]
    public delegate void ItemPanelPurchaseClickSignal();

    [Signal]
    public delegate void ItemPanelSaleClickSignal();

    [Signal]
    public delegate void ItemPanelWeaponEquipClickSignal();

    [Signal]
    public delegate void ItemPanelWeaponUnequipClickSignal();

    [Signal]
    public delegate void ItemPanelInventoryDropClickSignal();

    [Signal]
    public delegate void ItemPanelInventoryUseClickSignal();

    private Color _exitedColor = new Color(255f / 255f, 255f / 255f, 255f / 255f, 100f / 255f);
    private Color _enteredColor = new Color(202f / 255f, 122f / 255f, 74f / 255f, 100f / 255f);

    private ItemResource _itemResource;

    public enum ItemPanelType { PURCHASE, SALE, INVENTORY, WEAPON };

    private ItemPanelType _itemPanelType = ItemPanelType.PURCHASE;
    private bool _isOnPanel;
    private bool _isUsed;
    private bool _isEquipped;

    private int _inventoryIndex = -1;

    private Weapon.WeaponOrder _weaponOrder;

    private int _weaponIndex;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
    }

    public void Initialize(ItemResource itemResource, ItemPanelType itemPanelType, int inventoryIndex, Weapon.WeaponOrder weaponOrder, int weaponIndex)
    {
        SelfModulate = _exitedColor;
        _itemResource = itemResource;
        _isOnPanel = false;
        _isUsed = false;
        _isEquipped = false;
        _itemPanelType = itemPanelType;

        _inventoryIndex = inventoryIndex;
        _weaponOrder = weaponOrder;
        _weaponIndex = weaponIndex;

        ((RichTextLabel)(GetNode("Name"))).Text = "[" + _inventoryIndex + "] " + _itemResource.ItemID + " " + _itemResource.Name;
        ((Label)(GetNode("Price"))).Text = "COST: " + _itemResource.Price;
        ((RichTextLabel)(GetNode("Description"))).Text = _itemResource.Description;
        ((TextureRect)(GetNode("Image"))).Texture = _itemResource.ReferenceTexture;
        ((TextureRect)(GetNode("Image"))).RectScale = new Vector2(0.5f, 0.5f);

        if (ItemPanelType.PURCHASE == itemPanelType)
        {
            ((Button)GetNode("Purchase")).Show();
        }
        if (ItemPanelType.WEAPON == itemPanelType)
        {
            ((Button)GetNode("Equip")).Show();
        }
        if (ItemPanelType.INVENTORY == itemPanelType)
        {
            ((Button)GetNode("Drop")).Show();
            ((Button)GetNode("Sell")).Show();
            if (_itemResource.CurrentItemType == ItemResource.ItemType.USABLE)
            {
                ((Button)GetNode("Use")).Show();
            }
        }
    }

    public void SetUsed(bool isUsed)
    {
        if (_isUsed == isUsed)
        {
            return;
        }

        _isUsed = isUsed;
        ((Label)(GetNode("Used"))).Visible = isUsed;

        if (ItemPanelType.WEAPON == _itemPanelType)
        {
            // If in use, cannot equip
            ((Button)GetNode("Equip")).Visible = !isUsed;
        }

        if (ItemPanelType.INVENTORY == _itemPanelType)
        {
            ((Button)GetNode("Sell")).Visible = !isUsed;
            ((Button)GetNode("Drop")).Visible = !isUsed;
        }
    }

    public void SetEquipped(bool isEquipped)
    {
        if (_isEquipped == isEquipped)
        {
            return;
        }

        _isEquipped = isEquipped;
        if (ItemPanelType.WEAPON == _itemPanelType)
        {
            ((Label)(GetNode("Equipped"))).Visible = _isEquipped;
            // If is current equip, then can unequip
            ((Button)GetNode("Unequip")).Visible = _isEquipped;
        }
    }

    private void _drop()
    {
        EmitSignal(nameof(ItemPanelInventoryDropClickSignal), _inventoryIndex);
    }

    private void _sale()
    {
        EmitSignal(nameof(ItemPanelInventoryDropClickSignal), _inventoryIndex);
    }

    private void _purchase()
    {
        EmitSignal(nameof(ItemPanelPurchaseClickSignal), _itemResource);
    }

    private void _unequip()
    {
        EmitSignal(nameof(ItemPanelWeaponUnequipClickSignal), _weaponOrder, _weaponIndex);
    }

    private void _equip()
    {
        EmitSignal(nameof(ItemPanelWeaponEquipClickSignal), _inventoryIndex, _weaponOrder, _weaponIndex);
    }

    private void _use()
    {
        EmitSignal(nameof(ItemPanelInventoryUseClickSignal), _inventoryIndex);
    }

    private void mouseEntered()
    {
        SelfModulate = _enteredColor;
        _isOnPanel = true;

    }

    private void mouseExited()
    {
        SelfModulate = _exitedColor;
        _isOnPanel = false;

    }


}
