using Godot;

public class Observer : Node2D
{
    private RemoteTransform2D _remoteTransform2D;

    public override void _Ready()
    {
        _remoteTransform2D = (RemoteTransform2D)GetNode("CameraRemoteTransform");
    }

    public void gatherInput(float delta)
    {
        float yAxis = 0.0f;
        float xAxis = 0.0f;
        float moveFactor = 1000.0f;
        if (Input.IsActionPressed("turn_right"))
        {
            xAxis = +moveFactor * delta;
        }

        if (Input.IsActionPressed("turn_left"))
        {
            xAxis = -moveFactor * delta;
        }

        if (Input.IsActionPressed("forward"))
        {
            yAxis = -moveFactor * delta;
        }

        if (Input.IsActionPressed("backward"))
        {
            yAxis = +moveFactor * delta;
        }

        Position = Position + new Vector2(xAxis, yAxis);
    }

    public void SetCameraRemotePath(Camera2D camera)
    {
        _remoteTransform2D.RemotePath = _remoteTransform2D.GetPathTo(camera);
    }

    public override void _PhysicsProcess(float delta)
    {
        gatherInput(delta);
    }
}
