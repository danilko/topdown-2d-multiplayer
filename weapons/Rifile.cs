using Godot;
using System;

public class Rifile : Weapon
{
	public override void _Ready()
	{
		base._Ready();
	}

	protected override void FireEffect()
	{
			AnimationPlayer animationPlayer = (AnimationPlayer)GetNode("AnimationPlayer");
			animationPlayer.Play("MuzzleFlash");
	}
}
