using Godot;
using System;


public class PostProcess : CanvasLayer
{
    private float _time = 0.0f;
    private ColorRect _shader;
    public override void _Ready()
    {
        _shader = (ColorRect)GetNode("Shader");
    }

    public override void _Process(float delta)
    {
        _time += delta;

        ((ShaderMaterial)_shader.Material).SetShaderParam("camera_position", new Vector2());
        ((ShaderMaterial)_shader.Material).SetShaderParam("camera_size", GetViewport().Size);
    }

    public void shockwave(Vector2 globalPosition)
    {
        ((ShaderMaterial)_shader.Material).SetShaderParam("shockwave_start_time", _time);
        ((ShaderMaterial)_shader.Material).SetShaderParam("shockwave_position", globalPosition);
    }
}