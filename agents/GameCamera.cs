using Godot;
using System;

public class GameCamera : Camera2D
{
    private Boolean _screenShakeStart = false;

    [Export]
    private float _amplitude = 1.0f;

    private Timer _freqencyTimer;
    private Timer _durationTimer;
    private Tween _shakeTween;

    private int _pripority = 0;

    private Godot.RandomNumberGenerator random;


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _freqencyTimer = (Timer)GetNode("FrequencyTimer");
        _durationTimer = (Timer)GetNode("DurationTimer");
        _shakeTween = (Tween)GetNode("ShakeTween");

        random = new RandomNumberGenerator();
        Current = true;
    }

    public void StartScreenShake(float duration = 0.2f, float frequency = 15.0f, float amplitude = 15.0f, int pripority = 0)
    {
        if (pripority >= _pripority)
        {
            _pripority = pripority;
            _amplitude = amplitude;

            _freqencyTimer.WaitTime = 1 / frequency;
            _durationTimer.WaitTime = duration;

            _durationTimer.Start();
            _freqencyTimer.Start();

            _startScreenShake();
        }
    }

    private void _startScreenShake()
    {
        Vector2 randomVector = new Vector2(random.RandfRange(-_amplitude, _amplitude), random.RandfRange(-_amplitude, _amplitude));
        _shakeTween.InterpolateProperty(this, "offset", Offset, randomVector, _freqencyTimer.WaitTime, Tween.TransitionType.Sine, Tween.EaseType.InOut);

        _shakeTween.Start();
    }

    private void _resetScreenShake()
    {
        _shakeTween.InterpolateProperty(this, "offset", Offset, new Vector2(0, 0), _freqencyTimer.WaitTime, Tween.TransitionType.Sine, Tween.EaseType.InOut);

        _shakeTween.Start();

        _pripority = 0;
    }

    public void FrequencyTimerTimeout()
    {
        _startScreenShake();
    }

    public void DurationTimerTimeout()
    {
        _resetScreenShake();
        _freqencyTimer.Stop();
    }

}
