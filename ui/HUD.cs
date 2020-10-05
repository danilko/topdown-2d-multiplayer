using Godot;
using System.Collections.Generic;

public class HUD : CanvasLayer
{
    Network network;

    Texture barRed = (Texture)ResourceLoader.Load("res://assets/ui/red_button00.png");
    Texture barYellow = (Texture)ResourceLoader.Load("res://assets/ui/yellow_button00.png");
    Texture barGreen = (Texture)ResourceLoader.Load("res://assets/ui/green_button00.png");

    Dictionary<int, UIPlayerListEntry> uiPlayerListEntries = new Dictionary<int, UIPlayerListEntry>();
    Texture barTexture;

    public override void _Ready()
    {
        // Connect event handler to the player_list_changed signal
        network = (Network)GetNode("/root/NETWORK");
        network.Connect("PlayerListChangedSignal", this, nameof(onPlayerListChanged));
        network.Connect("PingUpdatedSignal", this, nameof(onPingUpdated));

        // Update the lblLocalPlayer label widget to display the local player name
        ((Label)GetNode("lblLocalPlayer")).Text = network.gamestateNetworkPlayer.name;

        // Hide the server info panel if on the server - it doesn't make any sense anyway
        if (GetTree().IsNetworkServer())
        {
            ((Panel)GetNode("PanelServerInfo")).Hide();
        }
    }

    private void onPlayerListChanged()
    {
        // Update the server name
        ((Label)GetNode("lblServerName")).Text = "Server: " + network.serverinfo.name;

        Node boxList = GetNode("boxList");
        foreach (Node node in boxList.GetChildren())
        {
            node.QueueFree();
        }

        uiPlayerListEntries.Clear();

        // Preload the entry control
        PackedScene entryClass = (PackedScene)GD.Load("res://ui/UIPlayerListEntry.tscn");


        foreach (KeyValuePair<int, NetworkPlayer> item in network.networkPlayers)
        {
            if (item.Key != network.gamestateNetworkPlayer.net_id)
            {
                UIPlayerListEntry playerEntryRoot = (UIPlayerListEntry)(entryClass.Instance());
                playerEntryRoot.setInfo(item.Value.name);
                uiPlayerListEntries.Add(item.Key, playerEntryRoot);
                boxList.AddChild(playerEntryRoot);
            }



        }
    }

    private void onPingUpdated(int peerId, float value)
    {
        if (peerId != network.gamestateNetworkPlayer.net_id)
        {
            // Updating the ping for local machine
            ((Label)GetNode("PanelServerInfo/lblPing")).Text = "Ping: " + (int)value;
        }
        else
        {
            if (uiPlayerListEntries.ContainsKey(peerId))
            {
                uiPlayerListEntries[peerId].setLatency(value);
            }
        }
    }
    public void _updateAmmoBar(int value)
    {
        TextureProgress ammoBar = (TextureProgress)GetNode("Margin/Container/AmmoBar");
        ammoBar.Value = value;

    }

    public void _updatePrimaryWeapon(Weapon.WeaponType weaponType)
    {
        ((Label)GetNode("lblPrimaryWeaponLabel")).Text = "" + weaponType.ToString();
        Sprite symbol = ((Sprite)GetNode("spPrimaryWeaponSymbol"));

        if (weaponType == Weapon.WeaponType.lasergun)
        {
            symbol.RegionRect = new Rect2(-1f, 263f, 96f, 93f);
            symbol.Scale = new Vector2(0.5f, 0.5f);
        }
        else if (weaponType == Weapon.WeaponType.rifile)
        {
            symbol.RegionRect = new Rect2(763f, 39f, 71f, 28f);
            symbol.Scale = new Vector2(1f, 1f);
        }
        else if (weaponType == Weapon.WeaponType.misslelauncher)
        {
            symbol.RegionRect = new Rect2(510f, 70f, 53f, 39f);
            symbol.Scale = new Vector2(1f, 1f);
        }
    }
    public void _updateSecondaryWeapon(Weapon.WeaponType weaponType)
    {

    }

    public void _updateHealthBar(int value)
    {
        barTexture = barGreen;

        if (value < 25)
        {
            barTexture = barRed;
        }
        else if (value < 60)
        {
            barTexture = barYellow;
        }

        TextureProgress healthBar = (TextureProgress)GetNode("Margin/Container/HealthBar");
        healthBar.TextureProgress_ = barTexture;

        Tween tween = (Tween)GetNode("Margin/Container/HealthBar/Tween");
        tween.InterpolateProperty(healthBar, "value", healthBar.Value,
        value, 0.2f,
        Tween.TransitionType.Linear, Tween.EaseType.InOut);
        tween.Start();
    }

    public void _updateDefeatedAgentBar(int value)
    {
        ((Label)GetNode("lblDefeatedAgentCount")).Text = "" + value;
    }
}
