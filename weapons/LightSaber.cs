using Godot;
using System;

public class LightSaber : Weapon
{
    [Export]
    int Damage = 50;

    private AnimationPlayer _animationPlayer;

    protected LightSaberAttack LightSaberAttack;

   public override void Initialize(GameWorld gameWorld, Agent agent, WeaponOrder weaponOrder, int weaponIndex)
    {
       base.Initialize(gameWorld, agent, weaponOrder, weaponIndex);
       _animationPlayer = (AnimationPlayer)GetNode("AnimationPlayer");

       // Rotated weapon
       _animationPlayer.Play("Attack_" + GetWeaponOrder());

       LightSaberAttack = (LightSaberAttack)GetNode("LightSaberAttack");
       LightSaberAttack.Initialize(Damage, agent);
    }

    public Team.TeamCode GetTeam()
    {
        return _team.CurrentTeamCode;
    }

    public override bool Fire(Agent targetAgent, Vector2 targetGlobalPosition)
    {
        if (Cooldown)
        {
            Cooldown = false;
            _animationPlayer.Play("Attack_" + GetWeaponOrder());
        }

        return false;
    }

    private void _onAnimationFinished(String animationName)
    {
        Cooldown = true;
    }



}
