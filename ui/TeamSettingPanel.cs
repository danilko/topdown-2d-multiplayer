using Godot;
using System;

public class TeamSettingPanel : Panel
{
    private SpinBox _spinBoxBudget;
    private SpinBox _spinBoxTotalCount;
    private CheckBox _checkboxAutoSpawnMember;

    private Team.TeamCode _teamCode;

    public override void _Ready()
    {
        _spinBoxBudget = (SpinBox)GetNode("SpinBoxTeamBudget");
        _spinBoxTotalCount = (SpinBox)GetNode("SpinBoxTotalUnitCount");
        _checkboxAutoSpawnMember = (CheckBox)GetNode("CheckBoxAutoSapwnMembers");
    }

    public void Initialize(Team.TeamCode teamCode)
    {
        _teamCode = teamCode;

        TextureRect textureRect = (TextureRect)GetNode("TextrectTeam");
        textureRect.Modulate = Team.TeamColor[(int)_teamCode];

        Label labelTeamName = (Label)GetNode("LabelTeamName");
        labelTeamName.Text = "" + _teamCode;
    }

    public Team.TeamCode GetTeamCode()
    {
        return _teamCode;
    }

    public int GetTeamBudget()
    {
        return (int)_spinBoxBudget.Value;
    }

    public bool GetTeamAutoSpawnMember()
    {
        return _checkboxAutoSpawnMember.Pressed;
    }

    public int GetTeamTotalUnitCount()
    {
        return (int)_spinBoxTotalCount.Value;
    }

}
