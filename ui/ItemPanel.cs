using Godot;
using System;

public class ItemPanel : Panel
{
    private Color _exitedColor = new Color(255f/255f,255f/255f,255f/255f,100f/255f);
    private Color _enteredColor = new Color(202f/255f,122f/255f,74f/255f,100f/255f);
    

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        this.SelfModulate = _exitedColor;
    }

    private void mouseEntered()
    {
       this.SelfModulate = _enteredColor;
    }

    private void mouseExited()
    {
        this.SelfModulate = _exitedColor;
    }

    
}
