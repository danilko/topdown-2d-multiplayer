using Godot;
using System;
using System.Collections.Generic;

public class HUD : CanvasLayer
{
    Network network;

    Texture barRed = (Texture)ResourceLoader.Load("res://assets/ui/red_button00.png");
    Texture barYellow = (Texture)ResourceLoader.Load("res://assets/ui/yellow_button00.png");
    Texture barGreen = (Texture)ResourceLoader.Load("res://assets/ui/green_button00.png");

    Dictionary<int, UIPlayerListEntry> uiPlayerListEntries = new Dictionary<int, UIPlayerListEntry>();
    Texture barTexture;

    bool lblMessage = false;


    public override void _Ready()
    {
        // Connect event handler to the player_list_changed signal
        network = (Network)GetNode("/root/NETWORK");
        network.Connect("PlayerListChangedSignal", this, nameof(onPlayerListChanged));
        network.Connect("PingUpdatedSignal", this, nameof(onPingUpdated));

        // Update the lblLocalPlayer label widget to display the local player name
        ((Label)GetNode("controlGame/lblLocalPlayer")).Text = network.gamestateNetworkPlayer.name;

        // Hide the server info panel if on the server - it doesn't make any sense anyway
        //if (GetTree().IsNetworkServer())
        //{
        //    ((Panel)GetNode("controlGame/PanelServerInfo")).Hide();
        //}

        // Hide other controls
        ((Control)GetNode("controlOverallMessage")).Visible = false;
        ((Control)GetNode("ControlPlayerList")).Visible = false;
    }

    private void onPlayerListChanged()
    {
        // Update the server name
        ((Label)GetNode("controlGame/lblServerName")).Text = "Server: " + network.serverinfo.name;

        Node boxList = GetNode("ControlPlayerList/Panel/boxList");
        foreach (Node node in boxList.GetChildren())
        {
            node.QueueFree();
        }

        uiPlayerListEntries.Clear();

        // Preload the entry control
        PackedScene entryClass = (PackedScene)GD.Load("res://ui/UIPlayerListEntry.tscn");

        foreach (KeyValuePair<int, NetworkPlayer> item in network.networkPlayers)
        {
            UIPlayerListEntry playerEntryRoot = (UIPlayerListEntry)(entryClass.Instance());

            String name = item.Value.name + "(" + item.Value.team + ")";

            if (item.Key != network.gamestateNetworkPlayer.net_id)
            {
                name = name + "(this client)";
            }

            playerEntryRoot.setInfo(name);
            uiPlayerListEntries.Add(item.Key, playerEntryRoot);
            boxList.AddChild(playerEntryRoot);
        }
    }

    private void onPingUpdated(int peerId, float value)
    {
        if (peerId != network.gamestateNetworkPlayer.net_id)
        {
            // Updating the ping for local machine
            ((Label)GetNode("controlGame/PanelServerInfo/lblPing")).Text = "Ping: " + (int)value;
        }
        else
        {
            if (uiPlayerListEntries.ContainsKey(peerId))
            {
                uiPlayerListEntries[peerId].setLatency(value);
            }
        }
    }

    private void _onNetworkRateUpdate(String message)
    {
        // Updating the network rate for local machine
        ((Label)GetNode("lblNetworkRate")).Text = "Network Rate: " + message;
    }

    public void UpdatePrimaryWeaponAmmo(int current, int max)
    {
        //TextureProgress ammoBar = (TextureProgress)GetNode("controlGame/Margin/Container/AmmoBar");
        //ammoBar.Value = value;
        ((Label)GetNode("controlGame/lblPrimaryWeaponAmmo")).Text = current + "/" + max;



        if (Mathf.Abs((float)current/(float)max) <= 0.1f)
        {
            // Change color during if ammo is under 10%
            ((Label)GetNode("controlGame/lblPrimaryWeaponAmmo")).Set("custom_colors/font_color", new Color("#ffc65b"));
        }
        else
        {
            ((Label)GetNode("controlGame/lblPrimaryWeaponAmmo")).Set("custom_colors/font_color", new Color("#96ff5b"));
        }

        // If not 0 ammo, disable the out ammo message
        if (current == 0)
        {
            ((Label)GetNode("controlGame/lblPrimaryWeaponAmmoOut")).Visible = true;
        }
        else
        {
            ((Label)GetNode("controlGame/lblPrimaryWeaponAmmoOut")).Visible = false;
        }
    }
    public void UpdatePrimaryWeaponAmmoOut()
    {
        ((Label)GetNode("controlGame/lblPrimaryWeaponAmmoOut")).Visible = true;
    }

    public void UpdatePrimaryWeaponReloadStart()
    {
        ((Label)GetNode("controlGame/lblPrimaryWeaponReloading")).Visible = true;
    }

    public void UpdatePrimaryWeaponReloadStop()
    {
        ((Label)GetNode("controlGame/lblPrimaryWeaponReloading")).Visible = false;
    }

    public void UpdateSecondaryWeaponAmmo(int current, int max)
    {
    }
    
    public void UpdateSecondaryWeaponAmmoOut()
    {
    }

    public void UpdateSecondaryWeaponReloadStart()
    {
    }

    public void UpdateSecondaryWeaponReloadStop()
    {
    }

    public void UpdatePrimaryWeapon(Weapon.WeaponType weaponType)
    {
        ((Label)GetNode("controlGame/lblPrimaryWeaponLabel")).Text = "" + weaponType.ToString();
        Sprite symbol = ((Sprite)GetNode("controlGame/spPrimaryWeaponSymbol"));

        if (weaponType == Weapon.WeaponType.LASER)
        {
            symbol.RegionRect = new Rect2(-1f, 263f, 96f, 93f);
            symbol.Scale = new Vector2(0.5f, 0.5f);
        }
        else if (weaponType == Weapon.WeaponType.RIFILE)
        {
            symbol.RegionRect = new Rect2(763f, 39f, 71f, 28f);
            symbol.Scale = new Vector2(1f, 1f);
        }
        else if (weaponType == Weapon.WeaponType.MISSLELAUNCHER)
        {
            symbol.RegionRect = new Rect2(510f, 70f, 53f, 39f);
            symbol.Scale = new Vector2(1f, 1f);
        }
    }
    public void UpdateSecondaryWeapon(Weapon.WeaponType weaponType)
    {
    }

    public void UpdateHealth(int value)
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

        TextureProgress healthBar = (TextureProgress)GetNode("controlGame/Margin/Container/HealthBar");
        healthBar.TextureProgress_ = barTexture;

        Tween tween = (Tween)GetNode("controlGame/Margin/Container/HealthBar/Tween");
        tween.InterpolateProperty(healthBar, "value", healthBar.Value,
        value, 0.2f,
        Tween.TransitionType.Linear, Tween.EaseType.InOut);
        tween.Start();
    }

    public void UpdateDefeatedAgent(int value)
    {
        ((Label)GetNode("controlGame/lblDefeatedAgentCount")).Text = "" + value;
    }

    public void OnPlayerDefeated()
    {
        ((Control)GetNode("controlGame")).Visible = false;
        ((Control)GetNode("controlOverallMessage")).Visible = true;

        ((AnimationPlayer)GetNode("AnimationPlayer")).Play("MessageAnnounce");
    }

    public void OnPlayerCreated()
    {
        ((Control)GetNode("controlOverallMessage")).Visible = false;
        lblMessage = false;
    }


    private void _onAnimationPlayerFinished(String animationName)
    {
        // As message is finished announce, able to close it
        if (animationName == "MessageAnnounce")
        {
            lblMessage = true;
        }
    }

    private void _onUpdateTimer(String message)
    {
        if (message.Contains("00:00:"))
        {
            ((Label)GetNode("lblTimerStatus")).Set("custom_colors/font_color", new Color("#ffc65b"));
        }
        else
        {
            ((Label)GetNode("lblTimerStatus")).Set("custom_colors/font_color", new Color("#96ff5b"));
        }


        ((Label)GetNode("lblTimerStatus")).Text = message;
    }

    public override void _PhysicsProcess(float delta)
    {
        if (lblMessage && Input.IsKeyPressed((int)Godot.KeyList.Space))
        {
            ((Control)GetNode("controlOverallMessage")).Visible = false;
            lblMessage = false;
        }

        if (Input.IsKeyPressed((int)Godot.KeyList.Tab))
        {
            ((Control)GetNode("controlGame")).Visible = false;
            ((Control)GetNode("ControlPlayerList")).Visible = true;
        }
        else
        {
            ((Control)GetNode("controlGame")).Visible = true;
            ((Control)GetNode("ControlPlayerList")).Visible = false;
        }
    }

}
