using Godot;
using System;

public class UIPlayerListEntry : MenuButton
{
    // Declare member variables here. Examples:
    // private int a = 2;
    // private string b = "text";

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {

    }

    public void setInfo(String pName)
    {
        ((Label)(GetNode("PlayerRow/lblName"))).Text = pName;
        //((TextureRect)(GetNode("PlayerRow/Icon"))).Texture = "";
    }

    public void setLatency(float latency)
    {
        ((Label)(GetNode("PlayerRow/lblLatency"))).Text = "(" + latency + ")";
    }
}
