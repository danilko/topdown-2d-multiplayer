using Godot;
using System;

public class AgentExplosionParticle : Node2D
{
    [Signal]
    public delegate void EffectCompleteSignal();

    private Boolean _trigger = false;

    public void SetTrigger(Boolean trigger)
    {
        // Only apply trigger if it is different then current
        if (_trigger != trigger)
        {
            _trigger = trigger;

                foreach (CPUParticles2D particle in GetNode("Particles").GetChildren())
                {
                    particle.Emitting = _trigger;
                }

                if(_trigger) {
    Timer timer =  (Timer)GetNode("Timer");
    timer.Start();
}
        }



    }

    public Boolean GetTrigger(Boolean trigger)
    {
        return _trigger;
    }

    private void _checkEmitting()
    {
        // If true, loop through and figure 
        if (_trigger)
        {
            Boolean complete = true;

            foreach (CPUParticles2D particle in GetNode("Particles").GetChildren())
            {
                if (particle.Emitting)
                {
                    complete = false;
                    break;
                }
            }

            if (complete)
            {
                EffectTimeout();
            }
        }
    }

    public override void _Process(float delta)
    {
        _checkEmitting();
    }

/**
* Force the timeout in case somehow particle is stuck
**/
    public void EffectTimeout()
    {
        EmitSignal(nameof(EffectCompleteSignal));
         _trigger = false;
         QueueFree();
    }

}
