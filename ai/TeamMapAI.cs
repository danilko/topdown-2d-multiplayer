using Godot;
using System;
using System.Collections.Generic;

public class TeamMapAI : Node2D
{
    [Signal]
    public delegate void TeamUnitUsageAmountChangeSignal();

    enum BaseCaptureStartOrder
    {
        FIRST,
        LAST
    }

    [Export]
    private BaseCaptureStartOrder baseCaptureStartOrder = BaseCaptureStartOrder.FIRST;

    private CapturableBaseManager _capturableBaseManager;
    private Team _team;

    private Node _unitsContainer;

    private GameWorld _gameWorld;
    private InventoryManager _inventoryManager;

    private PathFinding _pathFinding;

    [Export]
    private int _maxUnitUsageAmount = 1000;

    [Export]
    private int _maxConcurrentUnit = 10;

    [Export]
    private int _advancedWaitInterval = 20;

    private int _currentUnitUsageAmount;

    private int _unitCost = 100;

    private Timer _advancedTimer;

    [Export]
    private Boolean _autoSpawnMember = true;

    private  Godot.RandomNumberGenerator _randomNumberGenerator;

    public override void _Ready()
    {
        _team = (Team)GetNode("Team");
        _unitsContainer = GetNode("UnitsContainer");

        _currentUnitUsageAmount = _maxUnitUsageAmount;

        _advancedTimer = (Timer)GetNode("AdvancedTimer");
        _advancedTimer.WaitTime = _advancedWaitInterval;

        _randomNumberGenerator =  new Godot.RandomNumberGenerator();
    }

    public Boolean GetAutoSpawnMember()
    {
        return _autoSpawnMember;
    }

    public void SetAutoSpawnMember(Boolean autoSpawnMember)
    {
        _autoSpawnMember = autoSpawnMember;
    }

    public Team.TeamCode GetTeam()
    {
        return _team.CurrentTeamCode;
    }

    public int GetMaxUnitUsageAmount()
    {
        return _maxUnitUsageAmount;
    }

    public void SetMaxUnitUsageAmount(int maxUnitUsageAmount)
    {
        _maxUnitUsageAmount = maxUnitUsageAmount;
        _currentUnitUsageAmount = _maxUnitUsageAmount;

        EmitSignal(nameof(TeamUnitUsageAmountChangeSignal), _currentUnitUsageAmount);

        if (GetTree().NetworkPeer != null && GetTree().IsNetworkServer())
        {
            Rpc(nameof(_clientSetAmount), _currentUnitUsageAmount);
        }
    }

    public bool ChargeAmount(int chargeAmount)
    {
        if (_currentUnitUsageAmount - chargeAmount < 0)
        {
            return false;
        }
        else
        {
            _currentUnitUsageAmount = _currentUnitUsageAmount - chargeAmount;
            EmitSignal(nameof(TeamUnitUsageAmountChangeSignal), _currentUnitUsageAmount);

            if (GetTree().NetworkPeer != null && GetTree().IsNetworkServer())
            {
                Rpc(nameof(_clientSetAmount), _currentUnitUsageAmount);
            }

            return true;
        }
    }

    public void SyncTeamMapAICurrentUnitAmount(int rpcId)
    {
        if (rpcId != -1)
        {
            RpcId(rpcId, nameof(_clientSetAmount), _currentUnitUsageAmount);
        }
        else
        {
            Rpc(nameof(_clientSetAmount), _currentUnitUsageAmount);
        }
    }

    [Remote]
    private void _clientSetAmount(int amount)
    {
        if (!GetTree().IsNetworkServer())
        {
            _currentUnitUsageAmount = amount;
            EmitSignal(nameof(TeamUnitUsageAmountChangeSignal), _currentUnitUsageAmount);
        }
    }

    public bool isUnitUsageAmountAllowed()
    {
        if (_currentUnitUsageAmount < _unitCost || _currentUnitUsageAmount < 0)
        {
            return false;
        }

        return true;
    }

    public int GetMaxConcurrentUnit()
    {
        return _maxConcurrentUnit;
    }

    public void SetMaxConcurrentUnit(int maxConcurrentUnit)
    {
        _maxConcurrentUnit = maxConcurrentUnit;
    }

    public bool isConcurrentUnitAllowed()
    {
        if (_unitsContainer.GetChildren().Count >= _maxConcurrentUnit)
        {
            return false;
        }

        return true;
    }

    public bool isNewUnitAllow()
    {
        if (!isUnitUsageAmountAllowed())
        {
            return false;
        }

        if (!isConcurrentUnitAllowed())
        {
            return false;
        }

        if (GetSpawnPointFromCaptureBase() == null)
        {
            return false;
        }

        return true;
    }

    public void Initialize(GameWorld gameWorld, InventoryManager inventoryManager, CapturableBaseManager capturableBaseManager, Team.TeamCode team, PathFinding pathFinding)
    {
        _inventoryManager = inventoryManager;
        _capturableBaseManager = capturableBaseManager;
        _team.CurrentTeamCode = team;
        _gameWorld = gameWorld;
        _pathFinding = pathFinding;
        _advancedTimer.Start();

        CheckForCapturableBase();
    }

    public void CheckForCapturableBase()
    {
        CapturableBase capturableBase = _getNextCapturableBase();

        if (capturableBase != null)
        {
            _assignNextCapturableBaseToUnits(capturableBase);
        }
    }

    public void HandleCapturableBaseCaptured(CapturableBase capturable)
    {
        CheckForCapturableBase();
    }

    public Node GetUnitsContainer()
    {
        return _unitsContainer;
    }

    private CapturableBase _getNextCapturableBase()
    {
        List<CapturableBase> bases = _capturableBaseManager.GetCapturableBases();

        if (baseCaptureStartOrder == BaseCaptureStartOrder.LAST)
        {
            foreach (CapturableBase capturableBase in bases)
            {
                if (capturableBase.GetCaptureBaseTeam() != _team.CurrentTeamCode)
                {
                    return capturableBase;
                }
            }
        }
        else
        {
            for (int index = 0; index < bases.Count; index++)
            {
                if (bases[index].GetCaptureBaseTeam() != _team.CurrentTeamCode)
                {
                    return bases[index];
                }
            }
        }

        return null;
    }

    private void _assignNextCapturableBaseToUnits(CapturableBase capturableBase)
    {
        foreach (Node node in _unitsContainer.GetChildren())
        {
            if (node.HasMethod(nameof(AIAgent.GetAI)))
            {
                AI agentAI = ((AIAgent)node).GetAI();
                agentAI.SetNextBase(capturableBase);
                if (agentAI.getState() != AI.State.ENGAGE)
                {
                    agentAI.SetState(AI.State.ADVANCE);
                }
            }
        }
    }

    public Agent CreateUnit(String unitName, String displayName, Boolean enableAI)
    {
        Agent unit = null;

        if (enableAI)
        {
            unit = (AIAgent)((PackedScene)GD.Load("res://agents/AIAgent.tscn")).Instance();
        }
        else
        {
            unit = (Agent)((PackedScene)GD.Load("res://agents/Player.tscn")).Instance();
        }

        unit.Name = unitName;

        unit.SetNetworkMaster(1);

        unit.GlobalPosition = GetSpawnPointFromCaptureBase().GetRandomPositionWithinCaptureRadius();

        _unitsContainer.AddChild(unit);

        // Set the info afterward as some of these depend on child node to be available
        unit.Initialize(_gameWorld, unitName, displayName, this, _pathFinding);

        ChargeAmount(_unitCost);

        if (!enableAI)
        {
            _assignDefaultWeapon(unit);
        }
        else
        {
            _assignDefaultAIRandomCombine(unit);
        }

        return unit;
    }

    private void _assignDefaultWeapon(Agent agent)
    {
        if (GetTree().NetworkPeer == null || IsNetworkMaster())
        {
            // Add default wepaons
            _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-600"), agent.GetInventory());
            _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-800"), agent.GetInventory());
            _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-200"), agent.GetInventory());

            _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-600"), Weapon.WeaponOrder.Right, 0);
            _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-800"), Weapon.WeaponOrder.Left, 0);
            _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-200"), Weapon.WeaponOrder.Left, 1);
        }
    }

    private void _assignDefaultAIRandomCombine(Agent agent)
    {
        if (GetTree().NetworkPeer == null || IsNetworkMaster())
        {
            int weaponCombine = _randomNumberGenerator.RandiRange(0 , 5);

            if (weaponCombine == 0)
            {
                // Add Laser weapon
                _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-1000"), agent.GetInventory());
                _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-1000"), Weapon.WeaponOrder.Left, 0);
             }
            if (weaponCombine == 1)
            {
                // Add missle launcher + sheild
                _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-600"), agent.GetInventory());
                _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-300"), agent.GetInventory());

                _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-600"), Weapon.WeaponOrder.Right, 0);
                _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-300"), Weapon.WeaponOrder.Left, 0);
             }
            if (weaponCombine == 2)
            {
                // Add multi missle launcher + shield
                _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-600"), agent.GetInventory());
                _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-310"), agent.GetInventory());

                _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-600"), Weapon.WeaponOrder.Right, 0);
                _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-310"), Weapon.WeaponOrder.Left, 0);
             }
            if (weaponCombine == 3)
            {
                // Add multi missle launcher + shield
                _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-310"), agent.GetInventory());
                _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-310"), agent.GetInventory());

                _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-310"), Weapon.WeaponOrder.Right, 0);
                _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-310"), Weapon.WeaponOrder.Left, 0);
             }
            if (weaponCombine == 4)
            {
                // Add multi missle launcher + shield
                _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-800"), agent.GetInventory());
                _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-800"), agent.GetInventory());

                _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-800"), Weapon.WeaponOrder.Right, 0);
                _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-800"), Weapon.WeaponOrder.Left, 0);
             }
            if (weaponCombine == 5)
            {
                // Add rifile + sheild
                _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-600"), agent.GetInventory());
                _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-800"), agent.GetInventory());

                _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-600"), Weapon.WeaponOrder.Right, 0);
                _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-800"), Weapon.WeaponOrder.Left, 0);
            }
        }
    }


    public void RemoveUnit(String unitID)
    {
        Agent agent = GetUnit(unitID);


        if (agent == null || !IsInstanceValid(agent))
        {
            GD.Print("Cannoot remove invalid node from tree");
            return;
        }
        // Mark the node for deletion
        agent.Explode();
    }

    public CapturableBase GetSpawnPointFromCaptureBase()
    {
        CapturableBase targetCaptureBase = null;
        CapturableBase neutralCaptureBase = null;

        foreach (CapturableBase captureBase in _capturableBaseManager.GetCapturableBases())
        {
            if (captureBase.GetCaptureBaseTeam() == _team.CurrentTeamCode)
            {
                targetCaptureBase = captureBase;
                break;
            }

            if (captureBase.GetCaptureBaseTeam() == Team.TeamCode.NEUTRAL)
            {
                neutralCaptureBase = captureBase;
            }
        }

        // If not on same team base, try get a neutral base
        if (targetCaptureBase == null)
        {
            targetCaptureBase = neutralCaptureBase;
        }

        return targetCaptureBase;
    }

    public Agent GetUnit(String unitName)
    {
        if (_unitsContainer.HasNode(unitName))
        {
            return (Agent)_unitsContainer.GetNode(unitName);
        }
        else
        {
            foreach (Agent agent in _unitsContainer.GetChildren())
            {
                if (agent.GetUnitName().Equals(unitName))
                {
                    return agent;
                }
            }
        }

        return null;
    }
}
