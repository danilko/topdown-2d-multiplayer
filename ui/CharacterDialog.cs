using Godot;
using System;

public class CharacterDialog : Popup
{
    Timer _timer;

    Sprite _image;
    RichTextLabel _name;
    RichTextLabel _dialog;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _timer = (Timer)GetNode("Timer");
        _image = (Sprite)GetNode("Image");
        _name = (RichTextLabel)GetNode("ColorRect/MarginContainer/VBoxContainer/Name");
        _dialog = (RichTextLabel)GetNode("ColorRect/MarginContainer/VBoxContainer/ScrollContainer/Dialog");
    }

    public void Activate()
    {
        _timer.Stop();
        _timer.Start();
        _name.Text = "Test User Dialog Activate:";
        _dialog.Text = "Test User Dialog Dialog test test test test";
        Visible = true;
    }

    public void Deactivate()
    {
        _timer.Stop();
        Visible = false;
    }

    //  // Called every frame. 'delta' is the elapsed time since the previous frame.
    //  public override void _Process(float delta)
    //  {
    //      
    //  }
}
