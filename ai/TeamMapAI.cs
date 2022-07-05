using Godot;
using System;
using System.Collections.Generic;

public class TeamMapAI : Node2D
{
    [Signal]
    public delegate void TeamUnitUsageAmountChangeSignal();

    private List<String> weaponSetupDescriptionList;

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
    private int _advancedWaitInterval = 5;

    private int _currentUnitUsageAmount;

    private int _unitCost = 100;

    private int _totalUnitCount = 8;

    private Timer _advancedTimer;

    [Export]
    private Boolean _aiControl = true;

    private TeamMapAISetting.AILevel _teamAILevel;

    private Godot.RandomNumberGenerator _randomNumberGenerator;

    public override void _Ready()
    {
        _team = (Team)GetNode("Team");
        _unitsContainer = GetNode("UnitsContainer");

        _currentUnitUsageAmount = _maxUnitUsageAmount;

        _advancedTimer = (Timer)GetNode("AdvancedTimer");
        _advancedTimer.WaitTime = _advancedWaitInterval;

        _randomNumberGenerator = new Godot.RandomNumberGenerator();
		_randomNumberGenerator.Randomize();

        weaponSetupDescriptionList = new List<String>();
        _populateWeaponSetupDescriptionList();
    }

    public TeamMapAISetting.AILevel GetTeamAILevel()
    {
        return _teamAILevel;
    }

    public void SetTeamAILevel(TeamMapAISetting.AILevel teamAILevel)
    {
        _teamAILevel = teamAILevel;
    }

    public int GetTeamTotalUnitCount()
    {
        return _totalUnitCount;
    }

    public void SetTeamInitialUnitCount(int totalUnitCount)
    {
        _totalUnitCount = totalUnitCount;
    }

    public Boolean GetAIControl()
    {
        return _aiControl;
    }

    public void SetAIControl(Boolean aiControl)
    {
        _aiControl = aiControl;
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

        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {

            // Call locally for server
            _clientSetAmount(_currentUnitUsageAmount);

            if (GetTree().NetworkPeer != null)
            {
                Rpc(nameof(_clientSetAmount), _currentUnitUsageAmount);
            }
        }
    }

    public void ChargeUnitAmount()
    {
        ChargeAmount(_unitCost);
    }

    public bool ChargeAmount(int chargeAmount)
    {
        if (_currentUnitUsageAmount - chargeAmount < 0)
        {
            return false;
        }
        // Simulation will not cost money
        else if (_gameWorld.GetGameStateManager().GetGameStates().GetGameType() == GameStates.GameType.SIMULATION)
        {
            return true;
        }
        else
        {
            _currentUnitUsageAmount = _currentUnitUsageAmount - chargeAmount;

            if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
            {
                // Call locally for server
                _clientSetAmount(_currentUnitUsageAmount);

                if (GetTree().NetworkPeer != null)
                {
                    Rpc(nameof(_clientSetAmount), _currentUnitUsageAmount);
                }
            }

            return true;
        }
    }

    [Remote]
    private void _clientSetAmount(int amount)
    {
        _currentUnitUsageAmount = amount;
        EmitSignal(nameof(TeamUnitUsageAmountChangeSignal), _currentUnitUsageAmount);
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

        _gameWorld.GetAgentSpawnManager().Connect(nameof(AgentSpawnManager.AgentConfigSignal), this, nameof(_onAgentConfig));
    }

    private void _onAgentConfig(String unitID, Team.TeamCode teamCode)
    {
        // It is not for this team, just ignore it
        // If it is player, also ignore it (only make decision for AI)
        if (teamCode != _team.CurrentTeamCode || unitID.Contains(AgentSpawnManager.AgentPlayerPrefix))
        {
            return;
        }

        List<CapturableBase> baseList = _capturableBaseManager.GetAvailableBases(teamCode);


        // Only make a selection if base is availble
        if (baseList.Count > 1)
        {
            // Random select
            int selectedBase = _randomNumberGenerator.RandiRange(0, baseList.Count - 1);

            int maxWeaponSetupIndex = 1;

            if (_teamAILevel == TeamMapAISetting.AILevel.MEDIUM)
            {
                maxWeaponSetupIndex = 3;
            }

            if (_teamAILevel == TeamMapAISetting.AILevel.STRONG)
            {
                maxWeaponSetupIndex = GetMaxWeaponSetupCount();
            }

            int weaponCombine = _randomNumberGenerator.RandiRange(0, maxWeaponSetupIndex);

            _gameWorld.GetAgentSpawnManager().UpdateNewUnitConfig(unitID, baseList[selectedBase].GetCapturableBaseIndex(), weaponCombine);
        }
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

    public Agent CreateUnit(String unitID, String displayName, int captureBase, Boolean enableAI)
    {
        Agent unit = null;

        if (enableAI)
        {
            unit = (AIAgent)((PackedScene)GD.Load("res://agents/AIAgent.tscn")).Instance();

            if (_teamAILevel == TeamMapAISetting.AILevel.MEDIUM)
            {
                // Increased the detection by 200 (total to 1600)
                ((AIAgent)(unit)).DetectRadius += 200;
            }

            if (_teamAILevel == TeamMapAISetting.AILevel.STRONG)
            {
                // Increased the detection by 400 (total to 1600)
                ((AIAgent)(unit)).DetectRadius += 400;
            }
        }
        else
        {
            unit = (Agent)((PackedScene)GD.Load("res://agents/Player.tscn")).Instance();
        }

        unit.Name = unitID;

        unit.SetNetworkMaster(1);

        if (captureBase == -1 || _capturableBaseManager.GetCapturableBases()[captureBase].GetCaptureBaseTeam() != GetTeam())
        {
            // Use next possible base
            unit.GlobalPosition = GetSpawnPointFromCaptureBase().GetRandomPositionWithinCaptureRadius();
        }
        else
        {
            // Use the defined base
            unit.GlobalPosition = _capturableBaseManager.GetCapturableBases()[captureBase].GetRandomPositionWithinCaptureRadius();
        }

        _unitsContainer.AddChild(unit);

        // Set the info afterward as some of these depend on child node to be available
        unit.Initialize(_gameWorld, unitID, displayName, this, _pathFinding);

        return unit;
    }

    public int GetMaxWeaponSetupCount()
    {
        return weaponSetupDescriptionList.Count;
    }

    private void _populateWeaponSetupDescriptionList()
    {
        weaponSetupDescriptionList.Add("Fixed Output Rifile(SYC-800) + Sheild(SYC-600)");
        weaponSetupDescriptionList.Add("Missile Launcher(SYC-300) + Sheild(SYC-600)");
        weaponSetupDescriptionList.Add("Multi-Missile Launcher(SYC-310) + Sheild(SYC-600)");
        weaponSetupDescriptionList.Add("Multi-Missile Launcher(SYC-310) + Multi-Missile Launcher(SYC-310)");
        weaponSetupDescriptionList.Add("Rifile(SYC-800) + Rifile(SYC-800)");
        weaponSetupDescriptionList.Add("Variable Output Rifile(SYC-1000)");
    }

    public String GetWeaponSetupDescription(int weaponSetupIndex)
    {
        return weaponSetupDescriptionList[weaponSetupIndex];
    }

    public void AssignDefaultWeaponSetup(Agent agent, int weaponSetupIndex)
    {
        if (weaponSetupIndex == 0)
        {
            // Add rifile + sheild
            _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-600"), agent.GetInventory());
            _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-800"), agent.GetInventory());

            _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-600", false), Weapon.WeaponOrder.Right, 0);
            _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-800", false), Weapon.WeaponOrder.Left, 0);
        }
        if (weaponSetupIndex == 1)
        {
            // Add missle launcher + sheild
            _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-600"), agent.GetInventory());
            _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-300"), agent.GetInventory());

            _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-600", false), Weapon.WeaponOrder.Right, 0);
            _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-300", false), Weapon.WeaponOrder.Left, 0);
        }
        if (weaponSetupIndex == 2)
        {
            // Add multi missle launcher + shield
            _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-600"), agent.GetInventory());
            _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-310"), agent.GetInventory());

            _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-600", false), Weapon.WeaponOrder.Right, 0);
            _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-310", false), Weapon.WeaponOrder.Left, 0);
        }
        if (weaponSetupIndex == 3)
        {
            // Add two missle launchers
            _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-310"), agent.GetInventory());
            _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-310"), agent.GetInventory());

            _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-310", false), Weapon.WeaponOrder.Right, 0);
            _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-310", false), Weapon.WeaponOrder.Left, 0);
        }
        if (weaponSetupIndex == 4)
        {
            // Add two rifiles
            _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-800"), agent.GetInventory());
            _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-800"), agent.GetInventory());

            _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-800", false), Weapon.WeaponOrder.Right, 0);
            _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-800", false), Weapon.WeaponOrder.Left, 0);
        }
        if (weaponSetupIndex == 5)
        {
            // Add Laser weapon
            _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-1000"), agent.GetInventory());
            _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-1000", false), Weapon.WeaponOrder.Left, 0);
        }

        // Add short range weapon
        _inventoryManager.AddItem(_inventoryManager.GetPurchasableItemByID("SYC-200"), agent.GetInventory());
        _inventoryManager.EquipItem(agent.GetInventory(), agent.GetInventory().GetItemIndex("SYC-200", false), Weapon.WeaponOrder.Left, 1);
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

    public Agent GetUnit(String unitID)
    {
        foreach (Agent agent in _unitsContainer.GetChildren())
        {
            if (agent.GetUnitID().Equals(unitID))
            {
                return agent;
            }
        }

        return null;
    }
}
