using Godot;
using System;

public class UnitLaunchBaseSelectPanel : HBoxContainer
{
	[Signal]
	public delegate void BaseSelectSignal();

    private Button _button;
    private Label _label;
    private CapturableBase _capturableBase;

    public override void _Ready()
    {
        _button = (Button)GetNode("BaseButton");
        _label = (Label)GetNode("BaseName");
    }

    public void Initialize(CapturableBase capturableBase)
    {

        _capturableBase = capturableBase;

        _label.Text = "BASE: " + _capturableBase.Name;
    }

    public void SelectBase()
    {
        _button.Text = "SELECTED";
        _button.Disabled = true;

        EmitSignal(nameof(BaseSelectSignal), _capturableBase.Name);
    }

    public CapturableBase GetBase()
    {
        return _capturableBase;
    }

    public void UnSelectBase()
    {
        _button.Text = "SELECT";
        _button.Disabled = false;
    }
}
