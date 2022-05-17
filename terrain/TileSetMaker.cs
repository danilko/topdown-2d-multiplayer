using Godot;
using System;

public class TileSetMaker : Node2D
{
 
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Vector2 tileSize = new Vector2(128, 128);
        Sprite sprite = (Sprite)GetNode("Sprite");
        Texture texture = sprite.Texture;

        float txWidth = texture.GetWidth() / tileSize.x;
	    float txheight = texture.GetHeight() / tileSize.y;

	    TileSet ts = new TileSet();
        for (int x=0; x <= txWidth; x++)
        {
                for (int y=0; y <= txheight; y++){
                Rect2 region = new Rect2(x* tileSize.x, y * tileSize.y, tileSize.x, tileSize.y);
                    int id = x + y * 10;
                    ts.CreateTile(id);
                    ts.TileSetTexture(id, texture);
                    ts.TileSetRegion(id, region);
                }
        }		
    	ResourceSaver.Save("res://terrain/terrain_tiles.tres", ts);
    }
}