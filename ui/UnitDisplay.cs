using Godot;
using System;

public class UnitDisplay : Node2D
{
   
    Texture barRed = (Texture) ResourceLoader.Load("res://assets/ui/red_button00.png");
    Texture barYellow = (Texture) ResourceLoader.Load("res://assets/ui/yellow_button00.png");
    Texture barGreen = (Texture) ResourceLoader.Load("res://assets/ui/green_button00.png");
    
    Texture barTexture;
        public override void _Ready()
    {
    TextureProgress healthBar = (TextureProgress)GetNode("HealthBar");  
    healthBar.Hide();
        
    }
       public override void _Process(float delta)
       {
           GlobalRotation = 0;
       }

    public void _updateHealthBar(int value){
        barTexture = barGreen;
          TextureProgress healthBar = (TextureProgress)GetNode("HealthBar");  
        
        if(value < 100){
            healthBar.Show();
        }

        if(value < 25){
            barTexture = barRed;
        }
        else if (value < 60){
            barTexture = barYellow;
        }
        
       

       healthBar.Value = value;
        healthBar.TextureProgress_ = barTexture;
    
    }
}
