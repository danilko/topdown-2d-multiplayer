using Godot;
using System;

public class Pickup : Area2D
{
    ItemResource _itemResource;
    InventoryManager _inventoryManager;
    private Texture[] iconTextures = { (Texture)GD.Load("res://assets/wrench_white.png") };
    AudioStream musicClip = (AudioStream)GD.Load("res://assets/sounds/Future Weapons 2 - Source - ammunition_elements_13.wav");

    [Export]
    Vector2 amount = new Vector2(10, 25);

    public override void _Ready()
    {
        Sprite icon = (Sprite)GetNode("Icon");
        icon.Texture = iconTextures[0];
    }

    public void Initialize(InventoryManager inventoryManager, ItemResource itemResource)
    {
        _itemResource = itemResource;
        _inventoryManager = inventoryManager;
    }

    public void _onPickupBodyEntered(Node2D body)
    {
        AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
        audioManager.playSoundEffect(musicClip);

        if (body.HasMethod(nameof(Agent.Heal)))
        {
            Agent agent = (Agent)body;
            _inventoryManager.AddItem(_itemResource, agent.GetInventory());
        }

        QueueFree();
    }
}
