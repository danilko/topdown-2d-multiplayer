using Godot;
using System;

public class WeaponControl : Popup
{
    private TextureRect _weaponSymbol;
    private Label _weaponName;
    private Label _weaponHolder;
    private Timer _timer;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _weaponSymbol= (TextureRect)GetNode("MarginContainer/VBoxContainer/Symbol");
        _weaponName = ((Label)GetNode("MarginContainer/VBoxContainer/Name"));
        _weaponHolder = ((Label)GetNode("MarginContainer/VBoxContainer/Holder"));
        _timer = ((Timer)GetNode("Timer"));
    }

    public void UpdateWeapon(ItemResource itemResource, Weapon.WeaponOrder weaponOrder, int weaponIndex)
    {
        if (itemResource == null)
        {
            _weaponSymbol.Texture = null;
            _weaponName.Text = "NO WEAPON";
        }
        else
        {
            _weaponName.Text = itemResource.ItemID + " " + itemResource.Name;
            _weaponSymbol.Texture = itemResource.ReferenceTexture;
            _weaponSymbol.RectScale = new Vector2(0.25f, 0.25f);
        }

        _weaponHolder.Text = "[" + weaponOrder + "]" + "SLOT [" + weaponIndex + "]";

        // Stop existing timer and start again
        _timer.Stop();

        Visible = true;
        _timer.Start();
    }

    private void _onWeaponControlTimeout()
    {
        Hide();
    }


}
