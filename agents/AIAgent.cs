using Godot;
using System;

public class AIAgent : Agent
{

    [Export]
    public int TurretSpeed { get; set; }

    [Export]
    public float DetectRadius { get; set; }

    private int _speed;

    private Godot.Collections.Array members = null;

    private Vector2 _originalPathLineLocation;

    private AI _agentAI;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        base._Ready();

        RightWeaponAction = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;
        LeftWeaponAction = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;

        _speed = MaxSpeed;

        members = new Godot.Collections.Array();

        _originalPathLineLocation = GlobalPosition;

        _agentAI = (AI)GetNode("AI");
    }

    public override void Initialize(GameWorld gameWorld, String unitName, String displayName, TeamMapAI teamMapAI, PathFinding pathFinding)
    {
        base.Initialize(gameWorld, unitName, displayName, teamMapAI, pathFinding);
        if (GetTree().IsNetworkServer())
        {
            _agentAI.Initialize(_gameWorld, this, pathFinding, DetectRadius);
        }
    }

    public AI GetAI()
    {
        return _agentAI;
    }

    protected override void DisconnectWeapon(Weapon currentWeapon, Weapon.WeaponOrder weaponOrder)
    {
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
        if (GetTree().IsNetworkServer())
        {
            _agentAI.Control(delta);
            ((Label)(GetNode("UnitDisplay/Name"))).Text = GetDisplayName() + "(" + GetCurrentTeam() + ")" + " - " + _agentAI.getState();

        }
    }


    public override void _PhysicsProcess(float delta)
    {
        if (!Alive)
        {
            return;
        }

        if (GetTree().IsNetworkServer())
        {
            _Control(delta);
        }
    }

}