using Godot;
using System;

public class WeaponSlotPanel : Panel
{
    [Signal]
    public delegate void WeaponSlotPanelClickSignal();

    private Color _exitedColor = new Color(255f / 255f, 255f / 255f, 255f / 255f, 100f / 255f);
    private Color _enteredColor = new Color(202f / 255f, 122f / 255f, 74f / 255f, 100f / 255f);

    private ItemResource _itemResource;

    private Weapon.WeaponOrder _weaponOrder;

    private int _weaponIndex;

    private bool _isOnPanel;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
    }

    public void Initialize(ItemResource itemResource, Weapon.WeaponOrder weaponOrder, int weaponIndex)
    {
        SelfModulate = _exitedColor;
        _itemResource = itemResource;
        _isOnPanel = false;
        _weaponOrder = weaponOrder;
        _weaponIndex = weaponIndex;

        ((Label)(GetNode("Name"))).Text = _weaponOrder + " " + _weaponIndex;

        if (itemResource != null)
        {
            ((TextureRect)(GetNode("Image"))).Texture = _itemResource.ReferenceTexture;
            ((TextureRect)(GetNode("Image"))).RectScale = new Vector2(0.5f, 0.5f);
        }
        else
        {
            // No image
            ((TextureRect)(GetNode("Image"))).RectScale = Vector2.Zero;
        }
    }

    private void _guiInput(InputEvent inputEvent)
    {
        if (_isOnPanel && inputEvent.IsActionPressed("left_click"))
        {
            EmitSignal(nameof(WeaponSlotPanelClickSignal), _weaponOrder, _weaponIndex);
        }
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
