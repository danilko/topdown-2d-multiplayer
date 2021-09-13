using Godot;
using System;
using System.Collections.Generic;

public class HUD : CanvasLayer
{
    Network network;

    bool lblMessage = false;

    private Godot.Collections.Dictionary<Weapon.WeaponOrder, WeaponControl> _weaponControls;

    private Control _gameControl;
    private Control _miniMap;

    private Control _overallMessageControll;

    public override void _Ready()
    {
        _gameControl = (Control)(GetNode("GameControl"));
        _miniMap = ((MiniMap)_gameControl.GetNode("MiniMap"));
        _overallMessageControll = ((Control)GetNode("controlOverallMessage"));
        _overallMessageControll.Visible = false;

        _weaponControls = new Godot.Collections.Dictionary<Weapon.WeaponOrder, WeaponControl>();
        _weaponControls.Add(Weapon.WeaponOrder.Right, (WeaponControl)(_gameControl.GetNode("RightWeaponControl")));
        _weaponControls.Add(Weapon.WeaponOrder.Left, (WeaponControl)(_gameControl.GetNode("LeftWeaponControl")));

    }

    private void _onNetworkRateUpdate(String message)
    {
        // Updating the network rate for local machine
        ((Label)GetNode("lblNetworkRate")).Text = "Network Rate: " + message;
    }

    public void UpdateWeapon(ItemResource itemResource, Weapon.WeaponOrder weaponOrder, int weaponIndex)
    {
       _weaponControls[weaponOrder].UpdateWeapon(itemResource, weaponOrder, weaponIndex);
    }

    public void OnPlayerDefeated()
    {
        _gameControl.Visible = false;
        _overallMessageControll.Visible = true;

        ((AnimationPlayer)GetNode("AnimationPlayer")).Play("MessageAnnounce");
    }

    public void OnPlayerCreated()
    {
        _overallMessageControll.Visible = false;

        _gameControl.Visible = true;
        lblMessage = false;
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
            _overallMessageControll.Visible = false;
            lblMessage = false;
        }
    }

}
