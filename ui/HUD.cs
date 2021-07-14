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

    public void UpdateWeaponAmmo(int current, int max, Weapon.WeaponOrder weaponOrder)
    {
        //TextureProgress ammoBar = (TextureProgress)GetNode("controlGame/Margin/Container/AmmoBar");
        //ammoBar.Value = value;
        ((Label)GetNode("controlGame/" + weaponOrder + "WeaponControl/lblWeaponAmmo")).Text = current + "/" + max;


        if (Mathf.Abs((float)current / (float)max) <= 0.1f)
        {
            // Change color during if ammo is under 10%
            ((Label)GetNode("controlGame/" + weaponOrder + "WeaponControl/lblWeaponAmmo")).Set("custom_colors/font_color", new Color("#ffc65b"));
        }
        else
        {
            ((Label)GetNode("controlGame/" + weaponOrder + "WeaponControl/lblWeaponAmmo")).Set("custom_colors/font_color", new Color("#96ff5b"));
        }

        // If not 0 ammo, disable the out ammo message
        if (current == 0)
        {
            ((Label)GetNode("controlGame/" + weaponOrder + "WeaponControl/lblWeaponAmmoMessage")).Visible = true;
            ((Label)GetNode("controlGame/" + weaponOrder + "WeaponControl/lblWeaponAmmoMessage")).Text = "AMMO OUT";
        }
        else
        {
            ((Label)GetNode("controlGame/" + weaponOrder + "WeaponControl/lblWeaponAmmoMessage")).Visible = false;
        }
    }

    public void UpdateWeaponAmmoOut(Weapon.WeaponOrder weaponOrder)
    {
        ((Label)GetNode("controlGame/" + weaponOrder + "WeaponControl/lblWeaponAmmoMessage")).Visible = true;
        ((Label)GetNode("controlGame/" + weaponOrder + "WeaponControl/lblWeaponAmmoMessage")).Text = "AMMO OUT";
    }

    public void UpdateWeaponReload(Weapon.WeaponOrder weaponOrder, bool reload)
    {
        ((Label)GetNode("controlGame/" + weaponOrder + "WeaponControl/lblWeaponAmmoMessage")).Visible = reload;

        if (reload)
        {
            ((Label)GetNode("controlGame/" + weaponOrder + "WeaponControl/lblWeaponAmmoMessage")).Text = "RELOADING";
        }
    }


    public void UpdateWeapon(ItemResource itemResource, Weapon.WeaponOrder weaponOrder)
    {

        TextureRect symbol = (TextureRect)GetNode("controlGame/" + weaponOrder + "WeaponControl/textureWeaponSymbol");

        if (itemResource == null)
        {
            ((Label)GetNode("controlGame/" + weaponOrder + "WeaponControl/lblWeaponName")).Text = "NO WEAPON";
            symbol.RectScale = Vector2.Zero;
            ((Label)GetNode("controlGame/" + weaponOrder + "WeaponControl/lblWeaponAmmo")).Text = "";
            ((Label)GetNode("controlGame/" + weaponOrder + "WeaponControl/lblWeaponAmmoMessage")).Text = "";
        }
        else
        {
            ((Label)GetNode("controlGame/" + weaponOrder + "WeaponControl/lblWeaponName")).Text = itemResource.ItemID + " " + itemResource.Name;
            symbol.Texture = itemResource.ReferenceTexture;
            symbol.RectScale = new Vector2(0.5f, 0.5f);
        }
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
        ((MiniMap)GetNode("controlGame/MiniMap")).Visible = false;
        ((Control)GetNode("controlGame/LeftWeaponControl")).Visible = false;
        ((Control)GetNode("controlGame/RightWeaponControl")).Visible = false;

        ((Control)GetNode("controlOverallMessage")).Visible = true;

        ((AnimationPlayer)GetNode("AnimationPlayer")).Play("MessageAnnounce");
    }

    public void OnPlayerCreated()
    {
        ((Control)GetNode("controlOverallMessage")).Visible = false;

        ((Control)GetNode("controlGame")).Visible = true;
        ((MiniMap)GetNode("controlGame/MiniMap")).Visible = true;
        ((Control)GetNode("controlGame/LeftWeaponControl")).Visible = true;
        ((Control)GetNode("controlGame/RightWeaponControl")).Visible = true;
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

    public void UpdateTeamUnitUsageAmount(int cost)
    {
        ((Label)GetNode("lblTeamUnitUsageAmount")).Text = "" + cost;
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
