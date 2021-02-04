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

                foreach (Particles2D particle in GetChildren())
                {
                    particle.Emitting = _trigger;
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

            foreach (Particles2D particle in GetChildren())
            {
                if (particle.Emitting)
                {
                    complete = false;
                    break;
                }
            }

            if (complete)
            {
                EmitSignal(nameof(EffectCompleteSignal));
                _trigger = false;
            }
        }
    }

    public override void _Process(float delta)
    {
        _checkEmitting();
    }


}
