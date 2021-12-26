using Godot;
using System;
using System.Collections.Generic;

public class UnitLaunchSetupUI : Popup
{
    private UnitLaunchBaseSelectPanel _panelTemplate;
    private VBoxContainer _baseVBOXContainer;

    private Dictionary<String, UnitLaunchBaseSelectPanel> _basePanels;

    private CapturableBase _selectedCapturableBase;

    private CapturableBaseManager _capturableBaseManager;

    private GameWorld _gameWorld;

    private MiniMap _miniMap;

    private Team.TeamCode _playerTeamCode;

    private String _unitID;

    private Timer _timer;

    public override void _Ready()
    {
        _baseVBOXContainer = (VBoxContainer)GetNode("VBoxContainer/BaseVBoxContainer");
        _panelTemplate = (UnitLaunchBaseSelectPanel)_baseVBOXContainer.GetNode("UnitLaunchBaseSelectPanel");

        _basePanels = new Dictionary<string, UnitLaunchBaseSelectPanel>();

        _timer = (Timer)GetNode("SelectionTimer");
        _timer.WaitTime = AgentSpawnManager.ALLOW_UNIT_CONFIG_TIME;
    }

    public void Initalize(GameWorld gameWorld, MiniMap miniMap)
    {
        _gameWorld = gameWorld;
        _capturableBaseManager = gameWorld.GetCapturableBaseManager();
        _miniMap = miniMap;

        // Go through base setup
        foreach (CapturableBase capturableBase in _capturableBaseManager.GetCapturableBases())
        {
            UnitLaunchBaseSelectPanel basePanel = (UnitLaunchBaseSelectPanel)_panelTemplate.Duplicate();
            basePanel.Name = capturableBase.Name + "_panel";
            basePanel.Connect(nameof(UnitLaunchBaseSelectPanel.BaseSelectSignal), this, nameof(_selectBase));

            _baseVBOXContainer.AddChild(basePanel);

            // Need to initialize after add, otherwise label that used in initialize will be null
            basePanel.Initialize(capturableBase);
            basePanel.Show();

            // Add panel to dictionary
            _basePanels.Add(capturableBase.Name, basePanel);
        }
    }
    
    public void EnableSetup(String unitID, Team.TeamCode teamCode)
    {
        _unitID = unitID;
        _playerTeamCode = teamCode;
        _selectedCapturableBase = null;
        UpdateBaseSelection();

        Visible = true;
        Input.SetMouseMode(Input.MouseMode.Visible);

        _timer.Start();
    }

    public void UpdateBaseSelection()
    {
        // Go through base setup
        foreach (CapturableBase capturableBase in _capturableBaseManager.GetCapturableBases())
        {
            if (capturableBase.GetCaptureBaseTeam() == _playerTeamCode)
            {
                // Allow the panel to be selectable
                _basePanels[capturableBase.Name].Visible = true;

                // If no previous selection, update to the first identify base
                if (_selectedCapturableBase == null)
                {
                    _basePanels[capturableBase.Name].SelectBase();
                }
            }
            else
            {
                _basePanels[capturableBase.Name].Visible = false;
            }
        }
    }

    private void _selectBase(String baseName)
    {
        foreach (String currentBaseName in _basePanels.Keys)
        {
            if (currentBaseName != baseName)
            {
                // Not select base
                _basePanels[currentBaseName].UnSelectBase();
            }
            else
            {
                _selectedCapturableBase = _basePanels[currentBaseName].GetBase();
            }
        }

        _miniMap.SelectBase(baseName);
    }

    private void _confirmSelection()
    {
        if (_selectedCapturableBase != null)
        {
            _gameWorld.GetAgentSpawnManager().UpdateNewUnitConfig(_unitID, _selectedCapturableBase.GetCapturableBaseIndex());
        }

        Hide();
        _miniMap.Hide();
        Input.SetMouseMode(Input.MouseMode.Hidden);
    }

    public override void _Process(float delta)
    {
        // Only update if visible
        if (Visible)
        {
            // If original selection is no longer allowed, auto switch to new one
            if (_selectedCapturableBase != null && _selectedCapturableBase.GetCaptureBaseTeam() != _playerTeamCode)
            {
                _selectedCapturableBase = null;
            }

            UpdateBaseSelection();
        }
    }
}
