using Godot;
using System;

public class ItemPanel : Panel
{
    [Signal]
    public delegate void ItemPanelClickSignal();

    private Color _exitedColor = new Color(255f / 255f, 255f / 255f, 255f / 255f, 100f / 255f);
    private Color _enteredColor = new Color(202f / 255f, 122f / 255f, 74f / 255f, 100f / 255f);

    private ItemResource _itemResource;

    private bool _isOnPanel;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
    }

    public void Initialize(ItemResource itemResource)
    {
        SelfModulate = _exitedColor;
        _itemResource = itemResource;
        _isOnPanel = false;

        ((Label)(GetNode("Name"))).Text = _itemResource.Name;
        ((Label)(GetNode("Price"))).Text = "COST: " + _itemResource.Price;
        ((Label)(GetNode("Description"))).Text = _itemResource.Description;
        ((TextureRect)(GetNode("Image"))).Texture = _itemResource.ReferenceTexture;
        ((TextureRect)(GetNode("Image"))).RectScale = new Vector2(0.5f, 0.5f);
    }

    private void _guiInput(InputEvent inputEvent)
    {
        if (_isOnPanel && inputEvent.IsActionPressed("left_click"))
        {
            EmitSignal(nameof(ItemPanelClickSignal), _itemResource);
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
