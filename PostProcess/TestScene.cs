using Godot;
using System;

public class TestScene : Node2D
{
    // Declare member variables here. Examples:
    // private int a = 2;
    // private string b = "text";

    // Called when the node enters the scene tree for the first time.

    private PostProcess _postProcess;
    public override void _Ready()
    {
        _postProcess = (PostProcess)GetNode("PostProcess");
    }

//  // Called every frame. 'delta' is the elapsed time since the previous frame.
  public override void _Process(float delta)
  {
      	if(Input.IsActionJustPressed("left_click"))
          {
        _postProcess.shockwave(GetGlobalMousePosition());

		//PostProcess.shockwave(GetGlobalMousePosition());
          }

 }
}
