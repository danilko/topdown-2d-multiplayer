using Godot;
using System;

public class RemainParticles : Particles2D
{
    public void ParticleTimeout()
    {
        SpeedScale = 0;
        SetProcess(false);
        SetPhysicsProcess(false);
        SetProcessInput(false);
        SetProcessInternal(false);
        SetProcessUnhandledInput(false);
        SetProcessUnhandledKeyInput(false);
    }
}
