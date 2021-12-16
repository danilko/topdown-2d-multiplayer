using Godot;
using System;
using System.Collections.Generic;

public class AIAgent : Agent
{

    [Export]
    public int TurretSpeed { get; set; }

    [Export]
    public float DetectRadius { get; set; }

    private int _speed;

    private Vector2 _originalPathLineLocation;

    private AI _agentAI;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        base._Ready();

        RightWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;
        LeftWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;

        _speed = MaxSpeed;

        _originalPathLineLocation = GlobalPosition;

        _agentAI = (AI)GetNode("AI");
    }

    public override void Initialize(GameWorld gameWorld, String unitID, String displayName, TeamMapAI teamMapAI, PathFinding pathFinding)
    {
        base.Initialize(gameWorld, unitID, displayName, teamMapAI, pathFinding);
        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            _agentAI.Initialize(_gameWorld, this, pathFinding, DetectionZone);
        }
    }

    public AI GetAI()
    {
        return _agentAI;
    }

    protected override void DisconnectWeapon(Weapon currentWeapon, Weapon.WeaponOrder weaponOrder)
    {
        base.DisconnectWeapon(currentWeapon, weaponOrder);
        if (currentWeapon != null && _agentAI != null && IsInstanceValid(_agentAI))
        {
            // Deregister weapon with ai
            if (currentWeapon.IsConnected(nameof(Weapon.AmmoOutSignal), _agentAI, "_onWeaponNeedReload"))
            {
                currentWeapon.Disconnect(nameof(Weapon.AmmoOutSignal), _agentAI, "_onWeaponNeedReload");
                currentWeapon.Disconnect(nameof(Weapon.ReloadSignal), _agentAI, "_onWeaponReload");
            }
        }
    }



    protected override void ConnectWeapon(Weapon currentWeapon, Weapon.WeaponOrder weaponOrder)
    {
        base.ConnectWeapon(currentWeapon, weaponOrder);

        if (currentWeapon != null && _agentAI != null && IsInstanceValid(_agentAI))
        {
            // Register weapon with AI
            if (!currentWeapon.IsConnected(nameof(Weapon.AmmoOutSignal), _agentAI, "_onWeaponNeedReload"))
            {
                currentWeapon.Connect(nameof(Weapon.AmmoOutSignal), _agentAI, "_onWeaponNeedReload");
                currentWeapon.Connect(nameof(Weapon.ReloadSignal), _agentAI, "_onWeaponReload");
            }

            base.ConnectWeapon(currentWeapon, weaponOrder);
        }
    }

    public override void _Control(float delta)
    {
        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            _agentAI.Control(delta);
            ((Label)(GetNode("UnitDisplay/Name"))).Text = GetDisplayName() + "(" + GetTeam() + ")" + " - " + _agentAI.getState();

        }
    }

    public override void OnTargetAgentChange()
    {
        base.OnTargetAgentChange();

        _agentAI.OnTargetAgentChange(CurrentTargetAgent);
    }


    public override void _PhysicsProcess(float delta)
    {
        if (!Alive)
        {
            return;
        }

        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            _Control(delta);
        }
    }

}