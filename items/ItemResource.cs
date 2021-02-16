using Godot;
using System;

public class ItemResource : Resource
{
    [Export]
    public String Name {get;set;}

    [Export]
    public String Description {get;set;}

    [Export]
    public int Price {get;set;}

    [Export]
    public bool Stackable {get; set;}

    [Export]
    public int MaxStackableCount {get; set;}

    [Export]
    public PackedScene ReferencePackedScene {get; set;}
 
    [Export]
    public Texture ReferenceTexture {get; set;}
       
}