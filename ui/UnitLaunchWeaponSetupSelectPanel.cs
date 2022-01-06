using Godot;
using System;

public class UnitLaunchWeaponSetupSelectPanel : HBoxContainer
{

	[Signal]
	public delegate void WeaponSetupSelectSignal();

    private Button _button;
    private RichTextLabel _label;
    private int _weaponSetupIndex;

    public override void _Ready()
    {
        _button = (Button)GetNode("WeaponSetupButton");
        _label = (RichTextLabel)GetNode("WeaponSetupDescription");
    }

    public void Initialize(int weaponSetupIndex, String weaponSetupDescription)
    {
        _weaponSetupIndex = weaponSetupIndex;
        _label.Text = "Setup " + weaponSetupIndex + ": " + weaponSetupDescription;
    }

    public void SelectWeaponSetup()
    {
        _button.Text = "SELECTED";
        _button.Disabled = true;

        EmitSignal(nameof(WeaponSetupSelectSignal), _weaponSetupIndex);
    }

    public int GetWeaponSetupIndex()
    {
        return _weaponSetupIndex;
    }

    public void UnselectWeaponSetup()
    {
        _button.Text = "SELECT";
        _button.Disabled = false;
    }
}
