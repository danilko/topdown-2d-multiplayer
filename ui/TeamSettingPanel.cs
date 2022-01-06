using Godot;
using System;

public class TeamSettingPanel : Panel
{
    private SpinBox _spinBoxBudget;
    private SpinBox _spinBoxTotalCount;
    private CheckBox _checkboxAIControl;
    private OptionButton _optionsAILevel;

    private Team.TeamCode _teamCode;

    public override void _Ready()
    {
        _spinBoxBudget = (SpinBox)GetNode("SpinBoxTeamBudget");
        _spinBoxTotalCount = (SpinBox)GetNode("SpinBoxTotalUnitCount");
        _checkboxAIControl = (CheckBox)GetNode("CheckBoxAIControl");
        _optionsAILevel = (OptionButton)GetNode("OptAILevel");
    }

    public void Initialize(Team.TeamCode teamCode)
    {
        _teamCode = teamCode;

        TextureRect textureRect = (TextureRect)GetNode("TextrectTeam");
        textureRect.Modulate = Team.TeamColor[(int)_teamCode];

        Label labelTeamName = (Label)GetNode("LabelTeamName");
        labelTeamName.Text = "" + _teamCode;

        _populateTeamAILevels();
    }

    private void _populateTeamAILevels()
    {
        for (int index = 0; index <= (int)(TeamMapAISetting.AILevel.STRONG); index++)
        {
            TeamMapAISetting.AILevel aiLevel = (TeamMapAISetting.AILevel)index;
            _optionsAILevel.AddItem("" + aiLevel, index);
        }

        // Pre Select the 0 index
        _optionsAILevel.Select(0);
    }

    public Team.TeamCode GetTeamCode()
    {
        return _teamCode;
    }

    public TeamMapAISetting.AILevel GetAILevel()
    {
        return (TeamMapAISetting.AILevel)_optionsAILevel.Selected;
    }

    public int GetTeamBudget()
    {
        return (int)_spinBoxBudget.Value;
    }

    public bool GetTeamAIControl()
    {
        return _checkboxAIControl.Pressed;
    }

    public int GetTeamTotalUnitCount()
    {
        return (int)_spinBoxTotalCount.Value;
    }

}
