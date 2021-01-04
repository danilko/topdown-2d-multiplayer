using Godot;

public class Observer : Camera2D
{

    TileMap tileMap;
    Rect2 mapLimit;
    Vector2 mapCellSize;

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

    public void SetCameraLimit()
    {
        Current = true;   
        Zoom = new Vector2(2.0f, 2.0f);
    }

    public override void _Process(float delta)
    {
        gatherInput(delta);
    }
}
