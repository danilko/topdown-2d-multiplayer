using Godot;
using System;

public class RemoteWeaponController : Node2D
{
    // Declare member variables here. Examples:
    // private int a = 2;
    // private string b = "text";

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        RemoteWeapon remoteWeapon = (RemoteWeapon)GetNode("Containers/RemoteWeapon1");
        remoteWeapon.initialize((Node2D)GetNode("Target"));
        remoteWeapon.Activate();
    }

//  // Called every frame. 'delta' is the elapsed time since the previous frame.
  public override void _Process(float delta)
  {
      
  }

  
}
